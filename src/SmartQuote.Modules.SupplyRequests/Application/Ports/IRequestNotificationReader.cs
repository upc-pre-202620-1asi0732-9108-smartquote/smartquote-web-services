using SmartQuote.API.SupplyRequests.Application.Views;

namespace SmartQuote.API.SupplyRequests.Application.Ports;

public interface IRequestNotificationReader
{
    Task<IReadOnlyList<RequestNotificationView>> GetForRecipientAsync(Guid recipientId, bool unreadOnly, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid notificationId, Guid recipientId, CancellationToken cancellationToken = default);
}
