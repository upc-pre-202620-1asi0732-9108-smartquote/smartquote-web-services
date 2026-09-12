namespace SmartQuote.API.SupplyRequests.Application.Views;

public sealed record RequestNotificationView(
    Guid NotificationId,
    Guid PurchaseRequestId,
    string NewStatus,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
