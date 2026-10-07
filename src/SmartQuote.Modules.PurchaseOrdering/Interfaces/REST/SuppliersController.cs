using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.PurchaseOrdering.Interfaces.REST.Resources;
using SmartQuote.API.PurchaseOrdering.Interfaces.REST.Transform;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.Modules.PurchaseOrdering.Application;

namespace SmartQuote.Modules.PurchaseOrdering.Interfaces.REST;

[ApiController]
[Authorize(Roles = SmartQuoteRoles.PurchasingStaff)]
public class SuppliersController(PurchaseOrderApplicationService applicationService) : ControllerBase
{
    [HttpGet("api/v1/suppliers/{taxIdentifier}/performance")]
    [ProducesResponseType<SupplierPerformanceResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SupplierPerformanceResource>> GetPerformance(
        string taxIdentifier,
        CancellationToken cancellationToken)
    {
        var view = await applicationService.GetSupplierPerformanceAsync(taxIdentifier, cancellationToken);
        return Ok(DeliveryEvaluationResourceFromViewAssembler.ToResource(view));
    }
}
