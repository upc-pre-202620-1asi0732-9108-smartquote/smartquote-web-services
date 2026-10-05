using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.Modules.Auditing.Application;
using SmartQuote.Modules.Auditing.Interfaces.REST.Resources;

namespace SmartQuote.Modules.Auditing.Interfaces.REST;

[ApiController]
[Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
public class AuditController(AuditTrailService trail) : ControllerBase
{
    [HttpGet("api/v1/audit/{entityType}/{entityId:guid}")]
    [ProducesResponseType<IReadOnlyList<AuditEventResource>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AuditEventResource>>> Timeline(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var events = await trail.TimelineAsync(entityType, entityId, cancellationToken);
        return Ok(events.Select(audit => new AuditEventResource(
            audit.AuditEventId,
            audit.EntityType,
            audit.EntityId,
            audit.Action,
            audit.ActorId,
            audit.Reason,
            audit.OccurredAt)).ToList());
    }
}
