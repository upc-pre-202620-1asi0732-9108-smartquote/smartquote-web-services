using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.Modules.QuotationIntake.Application;

namespace SmartQuote.Modules.QuotationIntake.Interfaces.REST;

[ApiController, Authorize(Roles = SmartQuoteRoles.PurchaseAnalyst)]
[Route("api/v1/quotation-expiry-alerts")]
public sealed class QuotationExpiryAlertsController(QuotationExpiryService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, bool unreadOnly = false, int page = 1, int pageSize = 50) => Ok(await service.ListAsync(unreadOnly, page, pageSize, ct));
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct) { await service.ReadAsync(id, ct); return NoContent(); }
}
