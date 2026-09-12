using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;
using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Notifications;

/// <summary>
/// Persistence-only record backing the request_notifications table. Not a domain entity:
/// the SupplyRequests domain model (docs/1-supply-requests.puml) has no notification aggregate,
/// this is purely what IRequestNotificationPort's in-app adapter writes as a side effect.
/// </summary>
public class RequestNotificationRecord
{
    public Guid Id { get; private set; }
    public PurchaseRequestId PurchaseRequestId { get; private set; } = null!;
    public Guid RecipientId { get; private set; }
    public string NewStatus { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    private RequestNotificationRecord() { }

    public static RequestNotificationRecord Create(PurchaseRequestId purchaseRequestId, Guid recipientId, string newStatus, string message) =>
        new()
        {
            Id = Guid.NewGuid(),
            PurchaseRequestId = purchaseRequestId,
            RecipientId = recipientId,
            NewStatus = newStatus,
            Message = message,
            CreatedAt = DateTimeOffset.UtcNow
        };

    public void MarkAsRead()
    {
        ReadAt ??= DateTimeOffset.UtcNow;
    }
}
