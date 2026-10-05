namespace SmartQuote.API.PurchaseOrdering.Interfaces.REST.Resources;

public sealed record DeliveryEvaluationResource(
    Guid DeliveryEvaluationId,
    Guid PurchaseOrderId,
    string SupplierTaxIdentifier,
    int OnTimeScore,
    int QualityScore,
    string? Observations,
    Guid EvaluatedBy,
    DateTimeOffset EvaluatedAt);
