using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.QuotationIntake.Application.Views;

public sealed record QuotationUploadResult(PoultryQuoteId QuotationId, bool WasCreated);
