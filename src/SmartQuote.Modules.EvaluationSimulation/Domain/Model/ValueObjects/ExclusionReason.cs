namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record ExclusionReason(EvaluationCriterionId CriterionId, string Code, string Explanation);
