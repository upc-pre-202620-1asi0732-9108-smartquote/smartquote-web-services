namespace SmartQuote.API.PurchaseOrdering.Application.Views;

public sealed record AuditEventView(
    Guid AuditEventId,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt);
