using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Events;

public record PurchaseRequestSubmitted(PurchaseRequestId RequestId, DateTimeOffset OccurredAt) : IDomainEvent;
