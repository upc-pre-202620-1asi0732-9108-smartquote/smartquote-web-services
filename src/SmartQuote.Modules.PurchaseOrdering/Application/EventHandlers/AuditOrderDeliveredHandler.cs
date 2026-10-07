using SmartQuote.API.PurchaseOrdering.Domain.Model.Events;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Entities;

namespace SmartQuote.API.PurchaseOrdering.Application.EventHandlers;

public sealed class AuditOrderDeliveredHandler(AuditTrailService trail)
    : IDomainEventHandler<PurchaseOrderDelivered>
{
    public Task HandleAsync(PurchaseOrderDelivered domainEvent, CancellationToken cancellationToken = default) =>
        trail.RecordAsync(
            AuditEvent.PurchaseOrder,
            domainEvent.PurchaseOrderId.Value,
            "Delivered",
            domainEvent.DeliveredBy.Value,
            null,
            domainEvent.OccurredAt,
            cancellationToken);
}
