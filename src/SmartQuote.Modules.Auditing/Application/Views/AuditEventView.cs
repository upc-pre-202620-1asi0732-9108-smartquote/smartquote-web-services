namespace SmartQuote.Modules.Auditing.Application.Views;

public sealed record AuditEventView(
    Guid AuditEventId,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt);
