namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record CriterionResult(
    EvaluationCriterionId CriterionId,
    bool Passed,
    decimal NormalizedScore,
    decimal WeightedContribution,
    string Explanation);
