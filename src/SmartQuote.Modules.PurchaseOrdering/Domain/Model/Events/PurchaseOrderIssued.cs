using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Events;

public record PurchaseOrderIssued(
    PurchaseOrderId PurchaseOrderId,
    string SimulationRunId,
    string PurchaseRequestId,
    UserId ApprovedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;
