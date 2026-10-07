using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Application.Views;

namespace SmartQuote.API.SupplyRequests.Application;

public sealed class RequestNotificationService(
    IRequestNotificationReader reader,
    ICurrentUser currentUser)
{
    public Task<IReadOnlyList<RequestNotificationView>> GetMineAsync(
        bool unreadOnly,
        CancellationToken cancellationToken = default) =>
        reader.GetForRecipientAsync(currentUser.UserId, unreadOnly, cancellationToken);

    public Task MarkMineAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default) =>
        reader.MarkAsReadAsync(notificationId, currentUser.UserId, cancellationToken);
}
