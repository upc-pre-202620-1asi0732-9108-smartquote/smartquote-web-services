namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

public sealed record RequestNotificationResource(
    Guid NotificationId,
    Guid PurchaseRequestId,
    string NewStatus,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
