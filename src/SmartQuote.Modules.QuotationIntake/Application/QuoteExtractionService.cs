using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.QuotationIntake.Application.AgentContracts;
using SmartQuote.Modules.QuotationIntake.Application.OutboundServices;
using SmartQuote.Modules.QuotationIntake.Application.Ports;
using SmartQuote.Modules.QuotationIntake.Application.Views;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Commands;
using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.QuotationIntake.Application;

public class QuoteExtractionService(
    IPoultryQuoteRepository repository,
    IQuoteExtractionAgent extractionAgent,
    IPurchaseRequestReferenceReader requestReferenceReader,
    IQuotationDocumentStorage documentStorage,
    IQuotationIntakeUnitOfWork unitOfWork,
    IDomainEventDispatcher domainEventDispatcher,
    ICurrentUser currentUser,
    IConfiguration configuration,
    ILogger<QuoteExtractionService> logger) : IVerifiedQuotationSnapshotProvider
{
    private const int DefaultMaxDocumentSizeBytes = 15 * 1024 * 1024;

    public async Task<QuotationUploadResult> UploadAsync(
        UploadQuotationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (await requestReferenceReader.GetActiveAsync(command.RequestId, cancellationToken) is null)
            throw new DomainException($"Purchase request '{command.RequestId}' is not accepting quotations.");

        var normalizedContentType = ValidatePdf(command.ContentType, command.FileName, command.FileContent);

        var hash = ComputeSha256(command.FileContent);
        var requestId = command.RequestId.ToString();

        var existing = await repository.FindByRequestAndHashAsync(requestId, hash, cancellationToken);
        if (existing is not null)
            return new QuotationUploadResult(existing.Id, false);

        var storageKey = await documentStorage.SaveAsync(
            command.FileName,
            normalizedContentType,
            command.FileContent,
            cancellationToken);

        try
        {
            var quote = PoultryQuote.Create(
                new PurchaseRequestReference(requestId),
                new SupplierReference(command.SupplierId, command.SupplierBusinessName, command.SupplierTaxIdentifier),
                new SourceDocument(command.FileName, normalizedContentType, storageKey, hash));

            await repository.AddAsync(quote, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);

            await domainEventDispatcher.DispatchAsync(quote.DomainEvents, cancellationToken);
            quote.ClearDomainEvents();

            return new QuotationUploadResult(quote.Id, true);
        }
        catch
        {
            await documentStorage.DeleteAsync(storageKey, cancellationToken);
            throw;
        }
    }

    public async Task ProcessAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        var quote = await FindOrThrowAsync(quotationId, cancellationToken);
        quote.BeginExtraction();

        Exception? failure = null;
        try
        {
            var content = await documentStorage.GetAsync(quote.SourceDocument.StorageKey, cancellationToken);
            var document = new QuotationDocument(quote.SourceDocument.FileName, quote.SourceDocument.ContentType, content);
            var result = await extractionAgent.ExtractAsync(document, cancellationToken);
            var minimumConfidence = configuration.GetValue("AI:MinimumConfidence", 0.75m);

            ValidateExtraction(result);

            var data = new ExtractedQuotationData(
                quote.Supplier,
                result.ValidUntil,
                result.Currency?.Trim().ToUpperInvariant(),
                result.DeliveryLeadTimeDays,
                result.Lines.Select(line => new ExtractedLineData(
                    line.Description,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.UnitPrice,
                    line.Specifications
                        .Where(specification => !string.IsNullOrWhiteSpace(specification.Name) && !string.IsNullOrWhiteSpace(specification.Value))
                        .Select(specification => new QuotedSpecification(
                            specification.Name,
                            specification.Value,
                            specification.UnitOfMeasure))
                        .ToList())).ToList(),
                result.Fields.Select(field => new ExtractedFieldData(
                    field.FieldPath,
                    field.Value,
                    field.Confidence,
                    field.PageNumber,
                    field.TextReference,
                    field.IsResolved && field.Confidence >= minimumConfidence && !string.IsNullOrWhiteSpace(field.Value))).ToList());

            quote.ApplyExtraction(data);
        }
        catch (UnprocessableDocumentException exception)
        {
            quote.Reject("The quotation document could not be processed safely.");
            failure = exception;
        }
        catch (DomainException exception)
        {
            quote.Reject("The extracted quotation data violates a required business rule.");
            failure = new UnprocessableDocumentException("The extracted quotation data is invalid or incomplete.", exception);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Quotation extraction failed for {QuotationId}", quotationId);
            quote.Reject("The external extraction service is temporarily unavailable.");
            failure = new ExternalServiceUnavailableException(
                "Quotation extraction is temporarily unavailable. The document remains registered and can be retried.",
                exception);
        }

        await unitOfWork.CompleteAsync(cancellationToken);

        if (failure is not null)
            throw failure;
    }

    public async Task ConfirmAsync(
        Guid quotationId,
        IReadOnlyDictionary<Guid, Guid> lineMappings,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var quote = await FindOrThrowAsync(quotationId, cancellationToken);
        EnsureVersion(quote.Version, expectedVersion);

        var request = await requestReferenceReader.GetActiveAsync(Guid.Parse(quote.RequestReference.RequestId), cancellationToken)
            ?? throw new DomainException("The associated purchase request is not accepting quotation verification.");

        var unknownItems = lineMappings.Values.Where(itemId => !request.RequestedItemIds.Contains(itemId)).Distinct().ToList();
        if (unknownItems.Count > 0)
            throw new DomainException("Every mapped requested item must belong to the associated purchase request.");

        quote.Confirm(
            new UserId(currentUser.UserId),
            lineMappings.ToDictionary(mapping => mapping.Key, mapping => mapping.Value.ToString()));

        await unitOfWork.CompleteAsync(cancellationToken);

        await domainEventDispatcher.DispatchAsync(quote.DomainEvents, cancellationToken);
        quote.ClearDomainEvents();
    }

    public async Task CorrectFieldAsync(CorrectFieldCommand command, CancellationToken cancellationToken = default)
    {
        var quote = await FindOrThrowAsync(command.QuotationId, cancellationToken);
        EnsureVersion(quote.Version, command.ExpectedVersion);

        quote.CorrectField(
            new ExtractedFieldId(command.FieldId),
            command.Value,
            new UserId(currentUser.UserId),
            command.Reason);

        await unitOfWork.CompleteAsync(cancellationToken);
    }

    public async Task<PoultryQuoteView> GetExtractionAsync(
        Guid quotationId,
        CancellationToken cancellationToken = default) =>
        ToView(await FindOrThrowAsync(quotationId, cancellationToken));

    public async Task<IReadOnlyList<PoultryQuoteView>> GetForRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var quotes = await repository.GetForRequestAsync(requestId.ToString(), cancellationToken);
        return quotes.Select(ToView).ToList();
    }

    public async Task<IReadOnlyList<VerifiedQuotationSnapshot>> GetVerifiedForRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var quotes = await repository.GetVerifiedForRequestAsync(requestId, cancellationToken);
        return quotes.Select(ToSnapshot).ToList();
    }

    private async Task<PoultryQuote> FindOrThrowAsync(Guid quotationId, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(new PoultryQuoteId(quotationId), cancellationToken)
        ?? throw new NotFoundException($"Poultry quote '{quotationId}' was not found.");

    private string ValidatePdf(string contentType, string fileName, byte[] content)
    {
        var hasPdfExtension = Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        var hasAcceptedContentType = SourceDocument.IsSupportedUploadContentType(contentType);

        if (!hasAcceptedContentType || !hasPdfExtension)
            throw new UnsupportedContentTypeException("Only PDF quotation documents are accepted.");

        var maximum = configuration.GetValue("DocumentStorage:MaxQuotationSizeBytes", DefaultMaxDocumentSizeBytes);
        if (content.Length is 0 || content.Length > maximum)
            throw new PayloadTooLargeException($"Quotation documents must contain between 1 and {maximum} bytes.");

        ReadOnlySpan<byte> signature = "%PDF-"u8;
        if (content.Length < signature.Length || !content.AsSpan(0, signature.Length).SequenceEqual(signature))
            throw new UnsupportedContentTypeException("The uploaded file does not contain a valid PDF signature.");

        // The PDF signature and extension are authoritative when a generic MIME type is sent by a client.
        // Persist a canonical value so downstream extraction never depends on client-specific MIME behavior.
        return SourceDocument.AllowedContentType;
    }

    private static void ValidateExtraction(ExtractionResult result)
    {
        if (result.Fields.Any(field =>
                string.IsNullOrWhiteSpace(field.FieldPath) ||
                field.Confidence is < 0 or > 1 ||
                field.PageNumber <= 0))
            throw new UnprocessableDocumentException("The extraction provider returned invalid field evidence.");

        if (result.DeliveryLeadTimeDays is < 0)
            throw new UnprocessableDocumentException("The extraction provider returned a negative delivery lead time.");

        if (result.Currency is not null && (result.Currency.Trim().Length != 3 || !result.Currency.Trim().All(char.IsLetter)))
            throw new UnprocessableDocumentException("The extraction provider returned an invalid currency code.");
    }

    private static string ComputeSha256(byte[] content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    private static void EnsureVersion(long actual, long expected)
    {
        if (expected <= 0)
            throw new ArgumentException("Expected version must be greater than zero.");
        if (actual != expected)
            throw new ConflictException($"Expected version {expected}, but the current quotation version is {actual}.");
    }

    private static PoultryQuoteView ToView(PoultryQuote quote) => new(
        quote.Id,
        quote.RequestReference.RequestId,
        quote.Supplier.SupplierId,
        quote.Supplier.BusinessName,
        quote.Supplier.TaxIdentifier,
        quote.SourceDocument.FileName,
        quote.ValidUntil,
        quote.Currency,
        quote.DeliveryLeadTimeDays,
        quote.Status.ToString(),
        quote.Version,
        quote.VerifiedBy is null ? null : (Guid)quote.VerifiedBy,
        quote.VerifiedAt,
        quote.RejectionReason,
        quote.CreatedAt,
        quote.UpdatedAt,
        quote.Lines.Select(line => new QuotationLineView(
            line.Id,
            line.RequestedItemId,
            line.LineNumber,
            line.Description,
            line.Quantity,
            line.UnitOfMeasure,
            line.UnitPrice,
            line.Specifications.Select(specification => new QuotedSpecificationView(
                specification.Name,
                specification.Value,
                specification.UnitOfMeasure)).ToList())).ToList(),
        quote.Fields.Select(field => new ExtractedFieldView(
            field.Id,
            field.FieldPath,
            field.OriginalValue,
            field.CurrentValue,
            field.IsRequired,
            field.Confidence.Value,
            field.Source.PageNumber,
            field.Source.TextReference,
            field.Status.ToString(),
            field.Corrections.Select(correction => new FieldCorrectionView(
                correction.PreviousValue,
                correction.CorrectedValue,
                correction.CorrectedBy,
                correction.CorrectedAt,
                correction.Reason)).ToList())).ToList());

    private static VerifiedQuotationSnapshot ToSnapshot(PoultryQuote quote) => new(
        quote.Id.ToString(),
        quote.Version,
        quote.RequestReference.RequestId,
        quote.Supplier.SupplierId,
        quote.Supplier.BusinessName,
        quote.Supplier.TaxIdentifier,
        quote.Currency ?? throw new DomainException("Verified quotation currency is missing."),
        quote.Lines.Select(line => new VerifiedQuotationLineSnapshot(
            line.Id.ToString(),
            line.RequestedItemId ?? throw new DomainException("Verified quotation line mapping is missing."),
            line.LineNumber,
            line.Description ?? throw new DomainException("Verified quotation line description is missing."),
            line.Quantity ?? throw new DomainException("Verified quotation line quantity is missing."),
            line.UnitOfMeasure ?? throw new DomainException("Verified quotation line unit is missing."),
            line.UnitPrice ?? throw new DomainException("Verified quotation line price is missing."),
            line.Specifications.Select(specification => new VerifiedQuotationSpecificationSnapshot(
                specification.Name,
                specification.Value,
                specification.UnitOfMeasure)).ToList())).ToList(),
        quote.DeliveryLeadTimeDays ?? throw new DomainException("Verified quotation delivery lead time is missing."),
        quote.VerifiedAt ?? throw new DomainException("Verified quotation timestamp is missing."));
}
