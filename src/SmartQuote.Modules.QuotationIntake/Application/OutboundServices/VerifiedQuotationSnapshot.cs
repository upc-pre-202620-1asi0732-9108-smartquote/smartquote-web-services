namespace SmartQuote.Modules.QuotationIntake.Application.OutboundServices;

public record VerifiedQuotationSnapshot(
    string QuotationId,
    long Version,
    string PurchaseRequestId,
    string SupplierId,
    string SupplierBusinessName,
    string SupplierTaxIdentifier,
    string Currency,
    IReadOnlyList<VerifiedQuotationLineSnapshot> Lines,
    int DeliveryLeadTimeDays,
    DateTimeOffset VerifiedAt);

public record VerifiedQuotationLineSnapshot(
    string LineId,
    string? RequestedItemId,
    int LineNumber,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    IReadOnlyList<VerifiedQuotationSpecificationSnapshot> Specifications);

public record VerifiedQuotationSpecificationSnapshot(string Name, string Value, string UnitOfMeasure);
