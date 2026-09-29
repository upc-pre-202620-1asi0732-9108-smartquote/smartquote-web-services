using SmartQuote.Modules.EvaluationSimulation.Application.Views;
using SmartQuote.Modules.EvaluationSimulation.Interfaces.REST.Resources;

namespace SmartQuote.Modules.EvaluationSimulation.Interfaces.REST.Transform;

public static class SimulationResultResourceFromViewAssembler
{
    public static SimulationResultResource ToResource(SimulationResultView view) =>
        new(
            view.SimulationRunId,
            view.ScenarioId,
            view.CriteriaVersion,
            view.InputFingerprint,
            view.ExecutedAt,
            view.IsCurrent,
            view.Recommendation is null ? null : new RecommendationResource(view.Recommendation.QuotationId, view.Recommendation.Score, view.Recommendation.Explanation),
            view.ExchangeRate is null ? null : new ExchangeRateResource(
                view.ExchangeRate.SourceCurrency,
                view.ExchangeRate.TargetCurrency,
                view.ExchangeRate.Rate,
                view.ExchangeRate.RateType,
                view.ExchangeRate.PublishedOn,
                view.ExchangeRate.Source,
                view.ExchangeRate.RetrievedAt),
            view.Evaluations.Select(evaluation => new QuotationEvaluationResource(
                evaluation.QuotationId,
                evaluation.IsEligible,
                evaluation.TotalScore,
                evaluation.Rank,
                evaluation.OriginalTotal,
                evaluation.OriginalCurrency,
                evaluation.ComparisonTotal,
                evaluation.ComparisonCurrency,
                evaluation.ConversionApplied,
                evaluation.CriterionResults.Select(result => new CriterionResultResource(result.CriterionId, result.Passed, result.NormalizedScore, result.WeightedContribution, result.Explanation)).ToList(),
                evaluation.ExclusionReasons.Select(reason => new ExclusionReasonResource(reason.CriterionId, reason.Code, reason.Explanation)).ToList())).ToList());
}
