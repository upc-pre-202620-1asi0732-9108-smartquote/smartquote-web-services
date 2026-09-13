using System.Security.Cryptography;
using SmartQuote.Modules.QuotationIntake.Application.AgentContracts;
using SmartQuote.Modules.QuotationIntake.Application.Ports;

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.AI;

/// <summary>
/// Deterministic development adapter. It keeps local demos and integration tests usable
/// without sending documents to an external service. Production must select OpenAI.
/// </summary>
public sealed class StubQuoteExtractionAgent : IQuoteExtractionAgent
{
    public Task<ExtractionResult> ExtractAsync(
        QuotationDocument document,
        CancellationToken cancellationToken = default)
    {
        var shortHash = Convert.ToHexStringLower(SHA256.HashData(document.Content))[..8];
        var source = $"Deterministic local extraction ({shortHash})";

        return Task.FromResult(new ExtractionResult(
            "Local Demo Supplier",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
            "PEN",
            3,
            [new ExtractedLineResult(
                "Poultry supply from local development quotation",
                1,
                "unit",
                100,
                [new ExtractedSpecificationResult("documentReference", shortHash, string.Empty)])],
            [
                new ExtractedFieldResult("supplier.businessName", "Local Demo Supplier", 1m, 1, source, true),
                new ExtractedFieldResult("validUntil", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)).ToString("O"), 1m, 1, source, true),
                new ExtractedFieldResult("currency", "PEN", 1m, 1, source, true),
                new ExtractedFieldResult("deliveryLeadTimeDays", "3", 1m, 1, source, true),
                new ExtractedFieldResult("lines[0].description", "Poultry supply from local development quotation", 1m, 1, source, true),
                new ExtractedFieldResult("lines[0].quantity", "1", 1m, 1, source, true),
                new ExtractedFieldResult("lines[0].unitOfMeasure", "unit", 1m, 1, source, true),
                new ExtractedFieldResult("lines[0].unitPrice", "100", 1m, 1, source, true)
            ]));
    }
}
