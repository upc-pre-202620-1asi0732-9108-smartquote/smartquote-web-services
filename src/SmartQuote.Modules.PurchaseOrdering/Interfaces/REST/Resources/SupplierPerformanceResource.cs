namespace SmartQuote.API.PurchaseOrdering.Interfaces.REST.Resources;

public sealed record SupplierPerformanceResource(
    string SupplierTaxIdentifier,
    int EvaluationCount,
    decimal? AverageOnTimeScore,
    decimal? AverageQualityScore,
    decimal? OverallScore,
    DateTimeOffset? FirstEvaluatedAt,
    DateTimeOffset? LastEvaluatedAt,
    IReadOnlyList<DeliveryEvaluationResource> Evaluations);
