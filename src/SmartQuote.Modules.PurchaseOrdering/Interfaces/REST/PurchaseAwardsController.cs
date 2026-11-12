using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.Modules.PurchaseOrdering.Application;

namespace SmartQuote.Modules.PurchaseOrdering.Interfaces.REST;

[ApiController, Authorize(Roles = SmartQuoteRoles.PurchasingStaff)]
public sealed class PurchaseAwardsController(PurchaseAwardApplicationService service) : ControllerBase
{
    [HttpPost("api/v1/simulations/{runId:guid}/purchase-awards"), Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
    public async Task<IActionResult> Approve(Guid runId, [FromHeader(Name = "Idempotency-Key")] string key,
        ApproveAwardCommand command, CancellationToken ct)
    {
        var result = await service.ApproveAsync(runId, key, command, ct);
        return result.WasCreated ? CreatedAtAction(nameof(Get), new { id = result.Result.Award.Id }, result.Result) : Ok(result.Result);
    }
    [HttpGet("api/v1/purchase-awards/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));
    [HttpGet("api/v1/purchase-requests/{requestId:guid}/purchase-award")]
    public async Task<IActionResult> ForRequest(Guid requestId, CancellationToken ct) => Ok(await service.GetForRequestAsync(requestId, ct));
    [HttpGet("api/v1/simulations/{runId:guid}/purchase-award-budget-check")]
    public async Task<IActionResult> CheckBudget(Guid runId, CancellationToken ct) => Ok(await service.CheckBudgetAsync(runId, ct));
}
