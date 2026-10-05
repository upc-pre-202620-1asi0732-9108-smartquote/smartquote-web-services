using Microsoft.EntityFrameworkCore;
using SmartQuote.API.PurchaseOrdering.Auditing.Application.Views;
using SmartQuote.API.PurchaseOrdering.Auditing.Domain.Model;
using SmartQuote.API.PurchaseOrdering.Auditing.Infrastructure.Persistence.EFC;

namespace SmartQuote.API.PurchaseOrdering.Auditing.Application;

public sealed class AuditTrailService(AuditingDbContext context)
{
    public async Task RecordAsync(
        string entityType,
        Guid entityId,
        string action,
        Guid actorId,
        string? reason,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        context.AuditEvents.Add(AuditEvent.Record(entityType, entityId, action, actorId, reason, occurredAt));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEventView>> TimelineAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        if (!AuditEvent.AuditedEntityTypes.Contains(entityType))
            throw new ArgumentException($"Entity type '{entityType}' is not audited.", nameof(entityType));

        return await context.AuditEvents
            .AsNoTracking()
            .Where(audit => audit.EntityType == entityType && audit.EntityId == entityId)
            .OrderBy(audit => audit.OccurredAt)
            .ThenBy(audit => audit.Id)
            .Select(audit => new AuditEventView(
                audit.Id,
                audit.EntityType,
                audit.EntityId,
                audit.Action,
                audit.ActorId,
                audit.Reason,
                audit.OccurredAt))
            .ToListAsync(cancellationToken);
    }
}
