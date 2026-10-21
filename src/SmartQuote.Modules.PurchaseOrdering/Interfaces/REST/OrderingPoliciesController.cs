using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.Modules.PurchaseOrdering.Application;

namespace SmartQuote.Modules.PurchaseOrdering.Interfaces.REST;

public sealed record SupplierDecisionResource(string Reason, long ExpectedVersion);
public sealed record AuthorizeBudgetResource(decimal Amount, string Currency, string Reason, long ExpectedVersion);

[ApiController, Authorize(Roles = SmartQuoteRoles.PurchasingStaff)]
[ProducesResponseType(typeof(ProblemDetails), 400)]
[ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403)]
[ProducesResponseType(typeof(ProblemDetails), 404)]
[ProducesResponseType(typeof(ProblemDetails), 409)]
[ProducesResponseType(typeof(ProblemDetails), 422)]
public sealed class OrderingPoliciesController(OrderingPolicyService service) : ControllerBase
{
    [HttpGet("api/v1/suppliers")]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierAccount>), 200)]
    public async Task<ActionResult<IReadOnlyList<SupplierAccount>>> Suppliers(string? status = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        Ok(await service.ListSuppliersAsync(status, page, pageSize, ct));
    [HttpGet("api/v1/suppliers/{supplierId:guid}")]
    [ProducesResponseType<SupplierAccount>(200)]
    public async Task<ActionResult<SupplierAccount>> Supplier(Guid supplierId, CancellationToken ct) => Ok(await service.GetSupplierAsync(supplierId, ct));
    [HttpPost("api/v1/suppliers/{supplierId:guid}/enable"), Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
    [ProducesResponseType<SupplierAccount>(200)]
    public async Task<ActionResult<SupplierAccount>> Enable(Guid supplierId, SupplierDecisionResource resource, CancellationToken ct) =>
        Ok(await service.DecideSupplierAsync(supplierId, "enable", resource.Reason, resource.ExpectedVersion, ct));
    [HttpPost("api/v1/suppliers/{supplierId:guid}/suspend"), Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
    [ProducesResponseType<SupplierAccount>(200)]
    public async Task<ActionResult<SupplierAccount>> Suspend(Guid supplierId, SupplierDecisionResource resource, CancellationToken ct) =>
        Ok(await service.DecideSupplierAsync(supplierId, "suspend", resource.Reason, resource.ExpectedVersion, ct));
    [HttpPost("api/v1/suppliers/{supplierId:guid}/reactivate"), Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
    [ProducesResponseType<SupplierAccount>(200)]
    public async Task<ActionResult<SupplierAccount>> Reactivate(Guid supplierId, SupplierDecisionResource resource, CancellationToken ct) =>
        Ok(await service.DecideSupplierAsync(supplierId, "reactivate", resource.Reason, resource.ExpectedVersion, ct));
    [HttpPut("api/v1/purchase-requests/{requestId:guid}/budget"), Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
    [ProducesResponseType<PurchaseBudgetView>(200)]
    public async Task<ActionResult<PurchaseBudgetView>> Put(Guid requestId, AuthorizeBudgetResource resource, CancellationToken ct) =>
        Ok(await service.PutBudgetAsync(requestId, resource.Amount, resource.Currency, resource.Reason, resource.ExpectedVersion, ct));
    [HttpGet("api/v1/purchase-requests/{requestId:guid}/budget")]
    [ProducesResponseType<PurchaseBudgetView>(200)]
    public async Task<ActionResult<PurchaseBudgetView>> Budget(Guid requestId, CancellationToken ct) => Ok(await service.GetBudgetAsync(requestId, ct));
    [HttpGet("api/v1/simulations/{runId:guid}/budget-check")]
    [ProducesResponseType<BudgetCheckView>(200)]
    public async Task<ActionResult<BudgetCheckView>> Check(Guid runId, string quotationId, CancellationToken ct)
    {
        if (!Guid.TryParse(quotationId, out _)) throw new ArgumentException("A quotationId from this simulation is required.");
        return Ok(await service.CheckBudgetAsync(runId, quotationId, ct));
    }
}
