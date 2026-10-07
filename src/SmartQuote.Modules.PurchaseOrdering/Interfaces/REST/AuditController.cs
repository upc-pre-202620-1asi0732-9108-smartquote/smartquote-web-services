using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.PurchaseOrdering.Application;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Interfaces.REST.Resources;

namespace SmartQuote.API.PurchaseOrdering.Interfaces.REST;

[ApiController]
[Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
public class AuditController(AuditTrailService trail, IActorNameDirectory actorNames) : ControllerBase
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
        var names = await actorNames.GetDisplayNamesAsync(events.Select(audit => audit.ActorId), cancellationToken);
        return Ok(events.Select(audit => new AuditEventResource(
            audit.AuditEventId,
            audit.EntityType,
            audit.EntityId,
            audit.Action,
            audit.ActorId,
            audit.Reason,
            audit.OccurredAt,
            names.TryGetValue(audit.ActorId, out var name) ? name : null)).ToList());
    }
}
