using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.Events;

public record QuotationUploaded(PoultryQuoteId QuotationId, string RequestId, DateTimeOffset OccurredAt) : IDomainEvent;
