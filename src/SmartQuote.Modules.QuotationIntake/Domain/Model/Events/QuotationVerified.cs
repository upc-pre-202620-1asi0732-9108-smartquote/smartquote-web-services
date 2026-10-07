using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.Events;

public record QuotationVerified(PoultryQuoteId QuotationId, string RequestId, long Version, DateTimeOffset OccurredAt) : IDomainEvent;
