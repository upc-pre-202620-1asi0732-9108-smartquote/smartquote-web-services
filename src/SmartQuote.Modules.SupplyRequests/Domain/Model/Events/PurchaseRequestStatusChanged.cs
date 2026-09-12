using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Events;

public record PurchaseRequestStatusChanged(
    PurchaseRequestId RequestId,
    UserId RequesterId,
    RequestStatus PreviousStatus,
    RequestStatus NewStatus,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
