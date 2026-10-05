namespace SmartQuote.API.PurchaseOrdering.Auditing.Interfaces.REST.Resources;

public sealed record AuditEventResource(
    Guid AuditEventId,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt);
