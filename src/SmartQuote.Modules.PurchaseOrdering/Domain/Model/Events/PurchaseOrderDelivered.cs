using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Events;

public record PurchaseOrderDelivered(
    PurchaseOrderId PurchaseOrderId,
    string PurchaseRequestId,
    UserId DeliveredBy,
    DateTimeOffset OccurredAt) : IDomainEvent;
