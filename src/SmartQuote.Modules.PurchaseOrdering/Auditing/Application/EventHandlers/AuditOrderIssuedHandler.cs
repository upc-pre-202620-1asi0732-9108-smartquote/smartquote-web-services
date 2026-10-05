using SmartQuote.API.PurchaseOrdering.Domain.Model.Events;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.PurchaseOrdering.Auditing.Domain.Model;

namespace SmartQuote.API.PurchaseOrdering.Auditing.Application.EventHandlers;

public sealed class AuditOrderIssuedHandler(AuditTrailService trail)
    : IDomainEventHandler<PurchaseOrderIssued>
{
    public Task HandleAsync(PurchaseOrderIssued domainEvent, CancellationToken cancellationToken = default) =>
        trail.RecordAsync(
            AuditEvent.PurchaseOrder,
            domainEvent.PurchaseOrderId.Value,
            "Issued",
            domainEvent.ApprovedBy.Value,
            "Purchase order issued from an approved simulation decision.",
            domainEvent.OccurredAt,
            cancellationToken);
}
