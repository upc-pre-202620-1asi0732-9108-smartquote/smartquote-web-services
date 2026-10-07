using SmartQuote.API.Shared.Domain;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record SupplierReference
{
    public string SupplierId { get; }
    public string BusinessName { get; }
    public string TaxIdentifier { get; }

    public SupplierReference(string supplierId, string businessName, string taxIdentifier)
    {
        if (string.IsNullOrWhiteSpace(supplierId))
            throw new DomainException("Supplier id is required.");

        SupplierId = supplierId;
        BusinessName = businessName?.Trim() ?? string.Empty;
        TaxIdentifier = taxIdentifier?.Trim() ?? string.Empty;
    }
}
