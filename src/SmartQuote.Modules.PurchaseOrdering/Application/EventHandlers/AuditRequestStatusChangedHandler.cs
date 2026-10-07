using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Domain.Model.Events;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Entities;

namespace SmartQuote.API.PurchaseOrdering.Application.EventHandlers;

public sealed class AuditRequestStatusChangedHandler(AuditTrailService trail)
    : IDomainEventHandler<PurchaseRequestStatusChanged>
{
    public Task HandleAsync(PurchaseRequestStatusChanged domainEvent, CancellationToken cancellationToken = default) =>
        trail.RecordAsync(
            AuditEvent.PurchaseRequest,
            domainEvent.RequestId.Value,
            $"StatusChanged:{domainEvent.PreviousStatus}->{domainEvent.NewStatus}",
            domainEvent.ChangedBy.Value,
            domainEvent.Reason,
            domainEvent.OccurredAt,
            cancellationToken);
}
