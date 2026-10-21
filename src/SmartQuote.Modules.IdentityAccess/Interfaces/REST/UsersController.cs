using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.Modules.IdentityAccess.Application.OutboundServices;

namespace SmartQuote.Modules.IdentityAccess.Interfaces.REST;

[ApiController, Authorize(Roles = SmartQuoteRoles.PurchaseManager), Route("api/v1/iam/users")]
[ProducesResponseType(typeof(ProblemDetails), 400)]
[ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403)]
public sealed class UsersController(IUserDirectory directory) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DirectoryUser>), 200)]
    public async Task<ActionResult<IReadOnlyList<DirectoryUser>>> List(string role = "PurchaseAnalyst", string status = "Active",
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (role != SmartQuoteRoles.PurchaseAnalyst || status != "Active")
            throw new ArgumentException("This directory lists Active PurchaseAnalyst accounts only.");
        return Ok(await directory.ListActiveAnalystsAsync(page, pageSize, ct));
    }
}
