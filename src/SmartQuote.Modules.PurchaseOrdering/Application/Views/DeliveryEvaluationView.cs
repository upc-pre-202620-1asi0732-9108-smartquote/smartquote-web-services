namespace SmartQuote.API.PurchaseOrdering.Application.Views;

public record DeliveryEvaluationView(
    Guid DeliveryEvaluationId,
    Guid PurchaseOrderId,
    string SupplierTaxIdentifier,
    int OnTimeScore,
    int QualityScore,
    string? Observations,
    Guid EvaluatedBy,
    DateTimeOffset EvaluatedAt);
