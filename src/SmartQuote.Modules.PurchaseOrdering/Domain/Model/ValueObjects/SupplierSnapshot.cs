using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

public record SupplierSnapshot
{
    public string SupplierId { get; }
    public string BusinessName { get; }
    public string TaxIdentifier { get; }

    public SupplierSnapshot(string supplierId, string businessName, string taxIdentifier)
    {
        if (string.IsNullOrWhiteSpace(supplierId))
            throw new DomainException("Supplier id is required.");

        if (string.IsNullOrWhiteSpace(businessName))
            throw new DomainException("Supplier business name is required.");

        if (string.IsNullOrWhiteSpace(taxIdentifier))
            throw new DomainException("Supplier tax identifier is required.");

        SupplierId = supplierId;
        BusinessName = businessName;
        TaxIdentifier = taxIdentifier;
    }
}
