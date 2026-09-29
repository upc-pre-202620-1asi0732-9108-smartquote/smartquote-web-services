namespace SmartQuote.Modules.EvaluationSimulation.Interfaces.REST.Resources;

public record SimulationResultResource(
    Guid SimulationRunId,
    Guid ScenarioId,
    int CriteriaVersion,
    string InputFingerprint,
    DateTimeOffset ExecutedAt,
    bool IsCurrent,
    RecommendationResource? Recommendation,
    ExchangeRateResource? ExchangeRate,
    IReadOnlyList<QuotationEvaluationResource> Evaluations);

public record QuotationEvaluationResource(
    string QuotationId,
    bool IsEligible,
    decimal TotalScore,
    int? Rank,
    decimal OriginalTotal,
    string OriginalCurrency,
    decimal ComparisonTotal,
    string ComparisonCurrency,
    bool ConversionApplied,
    IReadOnlyList<CriterionResultResource> CriterionResults,
    IReadOnlyList<ExclusionReasonResource> ExclusionReasons);

public record ExchangeRateResource(
    string SourceCurrency,
    string TargetCurrency,
    decimal Rate,
    string RateType,
    DateOnly PublishedOn,
    string Source,
    DateTimeOffset RetrievedAt);

public record CriterionResultResource(Guid CriterionId, bool Passed, decimal NormalizedScore, decimal WeightedContribution, string Explanation);

public record ExclusionReasonResource(Guid CriterionId, string Code, string Explanation);

public record RecommendationResource(string QuotationId, decimal Score, string Explanation);
