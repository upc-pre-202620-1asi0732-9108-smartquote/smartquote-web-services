using SmartQuote.API.PurchaseOrdering.Domain.Model.Events;
using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.Auditing.Domain.Model;

namespace SmartQuote.Modules.Auditing.Application.EventHandlers;

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
