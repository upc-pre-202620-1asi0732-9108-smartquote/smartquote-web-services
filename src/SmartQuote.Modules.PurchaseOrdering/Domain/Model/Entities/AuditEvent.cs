using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Entities;

public sealed class AuditEvent
{
    public const string PurchaseRequest = nameof(PurchaseRequest);
    public const string PurchaseOrder = nameof(PurchaseOrder);

    public static readonly IReadOnlySet<string> AuditedEntityTypes =
        new HashSet<string>(StringComparer.Ordinal) { PurchaseRequest, PurchaseOrder };

    public Guid Id { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private AuditEvent() { }

    public static AuditEvent Record(
        string entityType,
        Guid entityId,
        string action,
        Guid actorId,
        string? reason,
        DateTimeOffset occurredAt)
    {
        if (!AuditedEntityTypes.Contains(entityType))
            throw new DomainException($"Entity type '{entityType}' is not audited.");

        if (string.IsNullOrWhiteSpace(action))
            throw new DomainException("An audit action is required.");

        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorId = actorId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            OccurredAt = occurredAt
        };
    }
}
