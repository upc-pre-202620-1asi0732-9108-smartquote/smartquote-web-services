namespace SmartQuote.Modules.EvaluationSimulation.Interfaces.REST.Resources;

public record CreateScenarioResource(string RequestId, IReadOnlyList<CriterionResource> Criteria);

public record CreateScenarioVersionResource(IReadOnlyList<CriterionResource> Criteria);

public record CriterionResource(
    string Name,
    string TargetField,
    string Category,
    string Mode,
    string Operator,
    string ExpectedValue,
    string UnitOfMeasure,
    decimal Weight,
    int DisplayOrder);
