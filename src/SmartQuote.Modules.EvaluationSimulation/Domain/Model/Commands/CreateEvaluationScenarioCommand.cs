using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.Commands;

public record CreateEvaluationScenarioCommand(string RequestId, IReadOnlyList<CriterionData> Criteria);

public record CreateNextScenarioVersionCommand(Guid ScenarioId, IReadOnlyList<CriterionData> Criteria);

public record CriterionData(
    string Name,
    string TargetField,
    CriterionCategory Category,
    CriterionMode Mode,
    ComparisonOperator Operator,
    string ExpectedValue,
    string UnitOfMeasure,
    decimal Weight,
    int DisplayOrder);
