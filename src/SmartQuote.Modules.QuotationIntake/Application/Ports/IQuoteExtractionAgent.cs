using SmartQuote.Modules.QuotationIntake.Application.AgentContracts;

namespace SmartQuote.Modules.QuotationIntake.Application.Ports;

public interface IQuoteExtractionAgent
{
    Task<ExtractionResult> ExtractAsync(QuotationDocument document, CancellationToken cancellationToken = default);
}
