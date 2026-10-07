namespace SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

public record ApprovedPurchaseDecision(
    string SimulationRunId,
    string PurchaseRequestId,
    string QuotationId,
    SupplierSnapshot Supplier,
    string Currency,
    IReadOnlyList<ApprovedPurchaseLine> Lines,
    DeliveryTerms DeliveryTerms,
    string InputFingerprint);

public record ApprovedPurchaseLine(
    string? SourceQuotationLineId,
    string? SourceRequestedItemId,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice);
