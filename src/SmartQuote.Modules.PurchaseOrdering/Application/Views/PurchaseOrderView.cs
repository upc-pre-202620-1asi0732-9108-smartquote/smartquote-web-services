namespace SmartQuote.API.PurchaseOrdering.Application.Views;

public record PurchaseOrderView(
    Guid PurchaseOrderId,
    string OrderNumber,
    string SimulationRunId,
    string PurchaseRequestId,
    string QuotationId,
    string InputFingerprint,
    string SupplierId,
    string SupplierBusinessName,
    string SupplierTaxIdentifier,
    Guid ApprovedBy,
    DateTimeOffset ApprovedAt,
    string Status,
    string Currency,
    int DeliveryLeadTimeDays,
    string DeliveryConditions,
    string DeliveryDestination,
    decimal Total,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PurchaseOrderLineView> Lines);

public record PurchaseOrderLineView(
    Guid LineId,
    int LineNumber,
    string? SourceQuotationLineId,
    string? SourceRequestedItemId,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice);
