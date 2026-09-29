namespace SmartQuote.Modules.EvaluationSimulation.Application.Views;

public record SimulationResultView(
    Guid SimulationRunId,
    Guid ScenarioId,
    int CriteriaVersion,
    string InputFingerprint,
    DateTimeOffset ExecutedAt,
    bool IsCurrent,
    RecommendationView? Recommendation,
    ExchangeRateView? ExchangeRate,
    IReadOnlyList<QuotationEvaluationView> Evaluations);

public record QuotationEvaluationView(
    string QuotationId,
    bool IsEligible,
    decimal TotalScore,
    int? Rank,
    decimal OriginalTotal,
    string OriginalCurrency,
    decimal ComparisonTotal,
    string ComparisonCurrency,
    bool ConversionApplied,
    IReadOnlyList<CriterionResultView> CriterionResults,
    IReadOnlyList<ExclusionReasonView> ExclusionReasons);

public record ExchangeRateView(
    string SourceCurrency,
    string TargetCurrency,
    decimal Rate,
    string RateType,
    DateOnly PublishedOn,
    string Source,
    DateTimeOffset RetrievedAt);

public record CriterionResultView(Guid CriterionId, bool Passed, decimal NormalizedScore, decimal WeightedContribution, string Explanation);

public record ExclusionReasonView(Guid CriterionId, string Code, string Explanation);

public record RecommendationView(string QuotationId, decimal Score, string Explanation);
