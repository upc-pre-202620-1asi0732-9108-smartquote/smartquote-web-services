using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.QuotationIntake.Application;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Commands;
using SmartQuote.Modules.QuotationIntake.Interfaces.REST.Resource;
using SmartQuote.Modules.QuotationIntake.Interfaces.REST.Transform;

namespace SmartQuote.Modules.QuotationIntake.Interfaces.REST;

[ApiController]
[Authorize(Roles = SmartQuoteRoles.PurchasingStaff)]
public class PoultryQuotesController(QuoteExtractionService extractionService) : ControllerBase
{
    [HttpPost("api/v1/purchase-requests/{requestId:guid}/quotations")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<PoultryQuoteResource>(StatusCodes.Status201Created)]
    [ProducesResponseType<PoultryQuoteResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PoultryQuoteResource>> Upload(
        Guid requestId,
        [FromForm] UploadQuotationResource supplier,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length > 15 * 1024 * 1024)
            throw new PayloadTooLargeException("Quotation documents cannot exceed 15 MB.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var command = new UploadQuotationCommand(
            requestId,
            supplier.SupplierId,
            supplier.SupplierBusinessName,
            supplier.SupplierTaxIdentifier,
            file.FileName,
            file.ContentType,
            stream.ToArray());

        var upload = await extractionService.UploadAsync(command, cancellationToken);
        var view = await extractionService.GetExtractionAsync(upload.QuotationId, cancellationToken);
        var resource = PoultryQuoteResourceFromViewAssembler.ToResource(view);

        return upload.WasCreated
            ? CreatedAtAction(nameof(GetExtraction), new { quotationId = resource.QuotationId }, resource)
            : Ok(resource);
    }

    [HttpPost("api/v1/purchase-requests/{requestId:guid}/quotations/batch")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<IReadOnlyList<BatchQuotationUploadItemResource>>(StatusCodes.Status207MultiStatus)]
    public async Task<ActionResult<IReadOnlyList<BatchQuotationUploadItemResource>>> UploadBatch(
        Guid requestId,
        [FromForm] UploadQuotationResource supplier,
        [FromForm] IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files.Count is 0 or > 20)
            throw new ArgumentException("A batch must contain between 1 and 20 quotation documents.");

        var results = new List<BatchQuotationUploadItemResource>(files.Count);
        foreach (var file in files)
        {
            try
            {
                if (file.Length > 15 * 1024 * 1024)
                    throw new PayloadTooLargeException("Quotation documents cannot exceed 15 MB.");

                await using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                var command = new UploadQuotationCommand(
                    requestId,
                    supplier.SupplierId,
                    supplier.SupplierBusinessName,
                    supplier.SupplierTaxIdentifier,
                    file.FileName,
                    file.ContentType,
                    stream.ToArray());

                var result = await extractionService.UploadAsync(command, cancellationToken);
                results.Add(new BatchQuotationUploadItemResource(
                    file.FileName,
                    result.QuotationId.Value,
                    result.WasCreated,
                    null,
                    null));
            }
            catch (UnsupportedContentTypeException exception)
            {
                results.Add(new BatchQuotationUploadItemResource(file.FileName, null, false, "unsupported_media_type", exception.Message));
            }
            catch (PayloadTooLargeException exception)
            {
                results.Add(new BatchQuotationUploadItemResource(file.FileName, null, false, "payload_too_large", exception.Message));
            }
            catch (DomainException exception)
            {
                results.Add(new BatchQuotationUploadItemResource(file.FileName, null, false, "domain_rule_violation", exception.Message));
            }
        }

        return StatusCode(StatusCodes.Status207MultiStatus, results);
    }

    [HttpGet("api/v1/purchase-requests/{requestId:guid}/quotations")]
    [ProducesResponseType<IReadOnlyList<PoultryQuoteResource>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PoultryQuoteResource>>> ListForRequest(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var views = await extractionService.GetForRequestAsync(requestId, cancellationToken);
        return Ok(views.Select(PoultryQuoteResourceFromViewAssembler.ToResource).ToList());
    }

    [HttpPost("api/v1/quotations/{quotationId:guid}/process")]
    [ProducesResponseType<PoultryQuoteResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PoultryQuoteResource>> Process(Guid quotationId, CancellationToken cancellationToken)
    {
        await extractionService.ProcessAsync(quotationId, cancellationToken);
        var view = await extractionService.GetExtractionAsync(quotationId, cancellationToken);
        return Ok(PoultryQuoteResourceFromViewAssembler.ToResource(view));
    }

    [HttpGet("api/v1/quotations/{quotationId:guid}")]
    [ProducesResponseType<PoultryQuoteResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PoultryQuoteResource>> GetExtraction(Guid quotationId, CancellationToken cancellationToken)
    {
        var view = await extractionService.GetExtractionAsync(quotationId, cancellationToken);
        return Ok(PoultryQuoteResourceFromViewAssembler.ToResource(view));
    }

    [HttpPost("api/v1/quotations/{quotationId:guid}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Confirm(
        Guid quotationId,
        [FromBody] ConfirmQuotationResource resource,
        CancellationToken cancellationToken)
    {
        var mappings = resource.LineMappings.ToDictionary(item => item.LineId, item => item.RequestedItemId);
        await extractionService.ConfirmAsync(quotationId, mappings, resource.ExpectedVersion, cancellationToken);
        return NoContent();
    }

    [HttpPut("api/v1/quotations/{quotationId:guid}/fields/{fieldId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> CorrectField(
        Guid quotationId,
        Guid fieldId,
        [FromBody] CorrectFieldResource resource,
        CancellationToken cancellationToken)
    {
        var command = new CorrectFieldCommand(
            quotationId,
            fieldId,
            resource.Value,
            resource.Reason,
            resource.ExpectedVersion);
        await extractionService.CorrectFieldAsync(command, cancellationToken);
        return NoContent();
    }
}
