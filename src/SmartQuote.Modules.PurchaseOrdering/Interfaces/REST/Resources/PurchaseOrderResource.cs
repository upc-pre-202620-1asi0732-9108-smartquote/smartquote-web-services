namespace SmartQuote.API.PurchaseOrdering.Interfaces.REST.Resources;

public record PurchaseOrderResource(
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
    IReadOnlyList<PurchaseOrderLineResource> Lines,
    SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects.ApprovalPolicyEvidence? PolicyEvidence = null);

public record PurchaseOrderLineResource(
    Guid LineId,
    int LineNumber,
    string? SourceQuotationLineId,
    string? SourceRequestedItemId,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice);
