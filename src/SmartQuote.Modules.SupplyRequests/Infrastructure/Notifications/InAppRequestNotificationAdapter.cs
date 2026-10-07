using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Application.Views;
using SmartQuote.API.SupplyRequests.Domain.Model.Events;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Notifications;

/// <summary>
/// In-app notification adapter backed by the request_notifications table
/// (docs/database-schema-documentation.md). The recipient is the original requester.
/// </summary>
public class InAppRequestNotificationAdapter(SupplyRequestsDbContext context, ILogger<InAppRequestNotificationAdapter> logger)
    : IRequestNotificationPort, IRequestNotificationReader
{
    public async Task NotifyStatusChangedAsync(PurchaseRequestStatusChanged domainEvent, CancellationToken cancellationToken = default)
    {
        var message = $"Your purchase request changed status from {domainEvent.PreviousStatus} to {domainEvent.NewStatus}. Reason: {domainEvent.Reason}";

        var notification = RequestNotificationRecord.Create(
            domainEvent.RequestId,
            domainEvent.RequesterId,
            domainEvent.NewStatus.ToString(),
            message);

        await context.RequestNotifications.AddAsync(notification, cancellationToken);

        logger.LogInformation(
            "Purchase request {RequestId} changed status from {PreviousStatus} to {NewStatus} (requester {RequesterId}): {Reason}",
            domainEvent.RequestId,
            domainEvent.PreviousStatus,
            domainEvent.NewStatus,
            domainEvent.RequesterId,
            domainEvent.Reason);
    }

    public async Task<IReadOnlyList<RequestNotificationView>> GetForRecipientAsync(
        Guid recipientId,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        var query = context.RequestNotifications
            .AsNoTracking()
            .Where(notification => notification.RecipientId == recipientId);

        if (unreadOnly)
            query = query.Where(notification => notification.ReadAt == null);

        return await query
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(100)
            .Select(notification => new RequestNotificationView(
                notification.Id,
                notification.PurchaseRequestId.Value,
                notification.NewStatus,
                notification.Message,
                notification.CreatedAt,
                notification.ReadAt))
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid recipientId, CancellationToken cancellationToken = default)
    {
        var notification = await context.RequestNotifications
            .FirstOrDefaultAsync(item => item.Id == notificationId && item.RecipientId == recipientId, cancellationToken)
            ?? throw new NotFoundException($"Notification '{notificationId}' was not found.");

        notification.MarkAsRead();
        await context.SaveChangesAsync(cancellationToken);
    }
}
