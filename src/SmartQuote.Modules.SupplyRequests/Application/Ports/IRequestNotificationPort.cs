using SmartQuote.API.SupplyRequests.Domain.Model.Events;

namespace SmartQuote.API.SupplyRequests.Application.Ports;

public interface IRequestNotificationPort
{
    Task NotifyStatusChangedAsync(PurchaseRequestStatusChanged domainEvent, CancellationToken cancellationToken = default);
}
