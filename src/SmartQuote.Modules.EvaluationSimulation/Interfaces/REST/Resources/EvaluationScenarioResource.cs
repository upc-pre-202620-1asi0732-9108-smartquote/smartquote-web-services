namespace SmartQuote.Modules.EvaluationSimulation.Interfaces.REST.Resources;

public record EvaluationScenarioResource(
    Guid ScenarioId,
    string RequestId,
    int Version,
    string Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? SupersedesScenarioId,
    IReadOnlyList<EvaluationCriterionResource> Criteria);

public record EvaluationCriterionResource(
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
