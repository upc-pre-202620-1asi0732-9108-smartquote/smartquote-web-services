using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.Commands;
using SmartQuote.API.SupplyRequests.Domain.Model.Entities;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Application;

public class PurchaseRequestCommandService(
    IPurchaseRequestRepository repository,
    ISupplyRequestsUnitOfWork unitOfWork,
    IDomainEventDispatcher domainEventDispatcher,
    IRequestAttachmentStorage attachmentStorage,
    ICurrentUser currentUser)
{
    private const int MaxAttachmentSizeBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedAttachmentTypes = ["application/pdf", "image/jpeg", "image/png"];

    public async Task<PurchaseRequestId> RegisterAsync(RegisterPurchaseRequestCommand command, CancellationToken cancellationToken = default)
    {
        var items = command.Items.Select(BuildRequestedItem);

        var request = PurchaseRequest.Create(
            new UserId(currentUser.UserId),
            command.RequiredDate,
            command.Priority,
            items);

        await repository.AddAsync(request, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);

        await domainEventDispatcher.DispatchAsync(request.DomainEvents, cancellationToken);
        request.ClearDomainEvents();

        return request.Id;
    }

    public async Task AddAttachmentAsync(AddAttachmentCommand command, CancellationToken cancellationToken = default)
    {
        var request = await repository.GetByIdAsync(new PurchaseRequestId(command.RequestId), cancellationToken)
            ?? throw new NotFoundException($"Purchase request '{command.RequestId}' was not found.");

        if (request.RequesterId.Value != currentUser.UserId)
            throw new UnauthorizedAccessException("Production specialists may only attach files to their own purchase requests.");

        EnsureVersion(request.Version, command.ExpectedVersion);

        ValidateAttachment(command.FileName, command.ContentType, command.Content);

        var storageKey = await attachmentStorage.SaveAsync(command.FileName, command.Content, cancellationToken);

        try
        {
            var attachment = RequestAttachment.Create(command.FileName, command.ContentType, storageKey, new UserId(currentUser.UserId));
            request.AddAttachment(attachment);
            await unitOfWork.CompleteAsync(cancellationToken);
        }
        catch
        {
            await attachmentStorage.DeleteAsync(storageKey, cancellationToken);
            throw;
        }
    }

    public async Task ChangeStatusAsync(ChangeRequestStatusCommand command, CancellationToken cancellationToken = default)
    {
        var request = await repository.GetByIdAsync(new PurchaseRequestId(command.RequestId), cancellationToken)
            ?? throw new NotFoundException($"Purchase request '{command.RequestId}' was not found.");

        EnsureVersion(request.Version, command.ExpectedVersion);
        request.ChangeStatus(command.NextStatus, new UserId(currentUser.UserId), command.Reason);

        await domainEventDispatcher.DispatchAsync(request.DomainEvents, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
        request.ClearDomainEvents();
    }

    private static RequestedItem BuildRequestedItem(RequestedItemData data)
    {
        var item = RequestedItem.Create(data.Description, data.Quantity, data.UnitOfMeasure);

        foreach (var requirement in data.Requirements)
            item.AddRequirement(TechnicalRequirement.Create(
                requirement.Name,
                requirement.Operator,
                requirement.ExpectedValue,
                requirement.UnitOfMeasure,
                requirement.IsMandatory));

        return item;
    }

    private static void EnsureVersion(long actual, long expected)
    {
        if (expected <= 0)
            throw new ArgumentException("Expected version must be greater than zero.");

        if (actual != expected)
            throw new ConflictException($"Expected version {expected}, but the current request version is {actual}.");
    }

    private static void ValidateAttachment(string fileName, string contentType, byte[] content)
    {
        if (!AllowedAttachmentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new UnsupportedContentTypeException("Only PDF, JPEG and PNG request attachments are accepted.");

        if (content.Length is 0 or > MaxAttachmentSizeBytes)
            throw new PayloadTooLargeException($"Attachment size must be between 1 byte and {MaxAttachmentSizeBytes} bytes.");

        var extension = Path.GetExtension(fileName);
        var hasValidSignature = contentType.ToLowerInvariant() switch
        {
            "application/pdf" => extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) && content.AsSpan().StartsWith("%PDF-"u8),
            "image/jpeg" => (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) &&
                            content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            "image/png" => extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                           content.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            _ => false
        };

        if (!hasValidSignature)
            throw new UnsupportedContentTypeException("Attachment content does not match its declared file type.");
    }
}
