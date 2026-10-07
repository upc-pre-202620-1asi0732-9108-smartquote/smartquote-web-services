namespace SmartQuote.Modules.EvaluationSimulation.Application.Views;

public record EvaluationScenarioView(
    Guid ScenarioId,
    string RequestId,
    int Version,
    string Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? SupersedesScenarioId,
    IReadOnlyList<EvaluationCriterionView> Criteria);

public record EvaluationCriterionView(
    Guid CriterionId,
    string Name,
    string TargetField,
    string Category,
    string Mode,
    string Operator,
    string ExpectedValue,
    string UnitOfMeasure,
    decimal Weight,
    int DisplayOrder);
