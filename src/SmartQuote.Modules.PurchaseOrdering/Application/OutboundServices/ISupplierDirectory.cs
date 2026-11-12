namespace SmartQuote.Modules.PurchaseOrdering.Application.OutboundServices;

public sealed record SupplierEligibilitySnapshot(Guid SupplierId, string TaxIdentifier, string Status, long Version);
public interface ISupplierDirectory
{
    Task<Guid> RegisterIdentifiedAsync(string taxIdentifier, string businessName, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierEligibilitySnapshot>> GetEligibilityAsync(IReadOnlyList<string> identifiers, CancellationToken ct = default);
}
