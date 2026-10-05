using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Domain.Model.Events;
using SmartQuote.Modules.Auditing.Domain.Model;

namespace SmartQuote.Modules.Auditing.Application.EventHandlers;

public sealed class AuditRequestSubmittedHandler(AuditTrailService trail)
    : IDomainEventHandler<PurchaseRequestSubmitted>
{
    public Task HandleAsync(PurchaseRequestSubmitted domainEvent, CancellationToken cancellationToken = default) =>
        trail.RecordAsync(
            AuditEvent.PurchaseRequest,
            domainEvent.RequestId.Value,
            "Submitted",
            domainEvent.RequesterId.Value,
            "Purchase request submitted.",
            domainEvent.OccurredAt,
            cancellationToken);
}
