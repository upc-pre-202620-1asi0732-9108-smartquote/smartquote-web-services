using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Events;

public record PurchaseRequestSubmitted(PurchaseRequestId RequestId, UserId RequesterId, DateTimeOffset OccurredAt) : IDomainEvent;
