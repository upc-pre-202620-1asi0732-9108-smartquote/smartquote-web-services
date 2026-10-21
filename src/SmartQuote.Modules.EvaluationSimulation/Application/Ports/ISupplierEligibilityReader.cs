namespace SmartQuote.Modules.EvaluationSimulation.Application.Ports;

public sealed record SupplierEligibilityData(Guid SupplierAccountId, string TaxIdentifier, string Status, long Version);
public interface ISupplierEligibilityReader
{
    Task<IReadOnlyList<SupplierEligibilityData>> ReadAsync(IReadOnlyList<string> taxIdentifiers, CancellationToken ct);
}
