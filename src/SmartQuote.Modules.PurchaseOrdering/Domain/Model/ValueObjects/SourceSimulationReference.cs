using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

public record SourceSimulationReference
{
    public string SimulationRunId { get; }
    public string PurchaseRequestId { get; }
    public string QuotationId { get; }
    public string InputFingerprint { get; }

    public SourceSimulationReference(string simulationRunId, string purchaseRequestId, string quotationId, string inputFingerprint)
    {
        if (!Guid.TryParse(simulationRunId, out _))
            throw new DomainException("Source simulation run id is required.");

        if (!Guid.TryParse(purchaseRequestId, out _))
            throw new DomainException("Source purchase request id is required.");

        if (!Guid.TryParse(quotationId, out _))
            throw new DomainException("Source quotation id is required.");

        if (inputFingerprint.Length != 64 || inputFingerprint.Any(character => !Uri.IsHexDigit(character)))
            throw new DomainException("Source input fingerprint must be a SHA-256 hexadecimal value.");

        SimulationRunId = simulationRunId;
        PurchaseRequestId = purchaseRequestId;
        QuotationId = quotationId;
        InputFingerprint = inputFingerprint;
    }
}
