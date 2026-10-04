using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Commands;
using SmartQuote.API.PurchaseOrdering.Interfaces.REST.Resources;
using SmartQuote.API.PurchaseOrdering.Interfaces.REST.Transform;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.Modules.PurchaseOrdering.Application;

namespace SmartQuote.Modules.PurchaseOrdering.Interfaces.REST;

[ApiController]
[Authorize]
public class PurchaseOrdersController(PurchaseOrderApplicationService applicationService) : ControllerBase
{
    [HttpPost("api/v1/simulations/{runId:guid}/quotations/{quotationId:guid}/purchase-orders")]
    [Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
    [ProducesResponseType<PurchaseOrderResource>(StatusCodes.Status201Created)]
    [ProducesResponseType<PurchaseOrderResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseOrderResource>> ApproveAndGenerate(
        Guid runId,
        Guid quotationId,
        [FromBody] ApproveAndGenerateResource resource,
        CancellationToken cancellationToken)
    {
        var command = new ApproveAndGenerateCommand(
            runId,
            quotationId.ToString(),
            resource.DeliveryConditions,
            resource.DeliveryDestination);
        var result = await applicationService.ApproveAndGenerateAsync(command, cancellationToken);
        var response = PurchaseOrderResourceFromViewAssembler.ToResource(result.Order);

        return result.WasCreated
            ? CreatedAtAction(nameof(GetById), new { purchaseOrderId = response.PurchaseOrderId }, response)
            : Ok(response);
    }

    [HttpGet("api/v1/purchase-orders/{purchaseOrderId:guid}")]
    [Authorize(Roles = SmartQuoteRoles.PurchasingStaff)]
    [ProducesResponseType<PurchaseOrderResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResource>> GetById(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var view = await applicationService.GetByIdAsync(purchaseOrderId, cancellationToken);
        return Ok(PurchaseOrderResourceFromViewAssembler.ToResource(view));
    }

    [HttpGet("api/v1/simulations/{runId:guid}/purchase-order")]
    [Authorize(Roles = SmartQuoteRoles.PurchasingStaff)]
    [ProducesResponseType<PurchaseOrderResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseOrderResource>> GetBySimulation(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var view = await applicationService.GetBySimulationAsync(runId.ToString(), cancellationToken);
        return Ok(PurchaseOrderResourceFromViewAssembler.ToResource(view));
    }

    [HttpGet("api/v1/purchase-requests/{requestId:guid}/purchase-order")]
    [Authorize(Roles = SmartQuoteRoles.PurchasingStaff)]
    [ProducesResponseType<PurchaseOrderResource>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResource>> GetByRequest(Guid requestId, CancellationToken cancellationToken)
    {
        var order = await applicationService.GetByRequestAsync(requestId, cancellationToken);
        return order is null ? NotFound() : Ok(PurchaseOrderResourceFromViewAssembler.ToResource(order));
    }
}
