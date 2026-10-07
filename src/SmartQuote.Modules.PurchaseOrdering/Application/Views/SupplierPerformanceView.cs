namespace SmartQuote.API.PurchaseOrdering.Application.Views;

public record SupplierPerformanceView(
    string SupplierTaxIdentifier,
    int EvaluationCount,
    decimal? AverageOnTimeScore,
    decimal? AverageQualityScore,
    decimal? OverallScore,
    DateTimeOffset? FirstEvaluatedAt,
    DateTimeOffset? LastEvaluatedAt,
    IReadOnlyList<DeliveryEvaluationView> Evaluations);
