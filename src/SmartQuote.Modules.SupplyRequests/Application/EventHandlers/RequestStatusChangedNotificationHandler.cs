using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Domain.Model.Events;

namespace SmartQuote.API.SupplyRequests.Application.EventHandlers;

public class RequestStatusChangedNotificationHandler(IRequestNotificationPort notificationPort)
    : IDomainEventHandler<PurchaseRequestStatusChanged>
{
    public Task HandleAsync(PurchaseRequestStatusChanged domainEvent, CancellationToken cancellationToken = default) =>
        notificationPort.NotifyStatusChangedAsync(domainEvent, cancellationToken);
}
