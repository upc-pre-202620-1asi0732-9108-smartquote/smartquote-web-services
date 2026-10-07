using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Application;
using SmartQuote.API.SupplyRequests.Domain.Model.Commands;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Transform;

namespace SmartQuote.API.SupplyRequests.Interfaces.REST;

[ApiController]
[Authorize]
[Route("api/v1/purchase-requests")]
public class PurchaseRequestsController(
    PurchaseRequestCommandService commandService,
    PurchaseRequestQueryService queryService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = SmartQuoteRoles.ProductionSpecialist)]
    [ProducesResponseType<PurchaseRequestResource>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PurchaseRequestResource>> Register(
        [FromBody] CreatePurchaseRequestResource resource,
        CancellationToken cancellationToken)
    {
        var command = CreatePurchaseRequestCommandFromResourceAssembler.ToCommand(resource);
        var requestId = await commandService.RegisterAsync(command, cancellationToken);

        var view = await queryService.GetByIdAsync(requestId, cancellationToken);
        var created = PurchaseRequestResourceFromViewAssembler.ToResource(view);

        return CreatedAtAction(nameof(GetById), new { requestId = created.RequestId }, created);
    }

    [HttpGet]
    [ProducesResponseType<PagedPurchaseRequestsResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedPurchaseRequestsResource>> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var parsedStatus = status is null
            ? (RequestStatus?)null
            : EnumResourceParser.Parse<RequestStatus>(status, nameof(status));

        var result = await queryService.ListAsync(parsedStatus, page, pageSize, cancellationToken);
        return Ok(new PagedPurchaseRequestsResource(
            result.Items.Select(PurchaseRequestResourceFromViewAssembler.ToResource).ToList(),
            result.Page,
            result.PageSize,
            result.TotalItems,
            result.TotalPages));
    }

    [HttpGet("{requestId:guid}")]
    [ProducesResponseType<PurchaseRequestResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseRequestResource>> GetById(Guid requestId, CancellationToken cancellationToken)
    {
        var view = await queryService.GetByIdAsync(requestId, cancellationToken);
        return Ok(PurchaseRequestResourceFromViewAssembler.ToResource(view));
    }

    [HttpGet("{requestId:guid}/history")]
    [ProducesResponseType<RequestHistoryResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestHistoryResource>> GetHistory(Guid requestId, CancellationToken cancellationToken)
    {
        var view = await queryService.GetHistoryAsync(requestId, cancellationToken);
        return Ok(RequestHistoryResourceFromViewAssembler.ToResource(view));
    }

    [HttpPut("{requestId:guid}/status")]
    [Authorize(Roles = SmartQuoteRoles.PurchasingStaff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ChangeStatus(
        Guid requestId,
        [FromBody] ChangeRequestStatusResource resource,
        CancellationToken cancellationToken)
    {
        var command = ChangeRequestStatusCommandFromResourceAssembler.ToCommand(requestId, resource);
        await commandService.ChangeStatusAsync(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{requestId:guid}/attachments")]
    [Authorize(Roles = SmartQuoteRoles.ProductionSpecialist)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> AddAttachment(
        Guid requestId,
        IFormFile file,
        [FromForm] long expectedVersion,
        CancellationToken cancellationToken)
    {
        if (file.Length is 0 or > 10 * 1024 * 1024)
            throw new PayloadTooLargeException("Attachment size must be between 1 byte and 10 MB.");

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        var command = new AddAttachmentCommand(
            requestId,
            file.FileName,
            file.ContentType,
            memory.ToArray(),
            expectedVersion);

        await commandService.AddAttachmentAsync(command, cancellationToken);
        return NoContent();
    }
}
