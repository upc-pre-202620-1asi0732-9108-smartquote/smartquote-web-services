using SmartQuote.Modules.EvaluationSimulation.Application.Views;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Commands;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;
using SmartQuote.Modules.EvaluationSimulation.Interfaces.REST.Resources;

namespace SmartQuote.Modules.EvaluationSimulation.Interfaces.REST.Transform;

public static class ScenarioResourceTransforms
{
    public static CreateEvaluationScenarioCommand ToCommand(CreateScenarioResource resource) =>
        new(resource.RequestId, resource.Criteria.Select(ToCriterionData).ToList());

    public static CreateNextScenarioVersionCommand ToCommand(Guid scenarioId, IReadOnlyList<CriterionResource> criteria) =>
        new(scenarioId, criteria.Select(ToCriterionData).ToList());

    private static CriterionData ToCriterionData(CriterionResource resource) =>
        new(
            resource.Name,
            resource.TargetField,
            ParseEnum<CriterionCategory>(resource.Category, nameof(resource.Category)),
            ParseEnum<CriterionMode>(resource.Mode, nameof(resource.Mode)),
            ParseEnum<ComparisonOperator>(resource.Operator, nameof(resource.Operator)),
            resource.ExpectedValue,
            resource.UnitOfMeasure,
            resource.Weight,
            resource.DisplayOrder);

    private static TEnum ParseEnum<TEnum>(string value, string fieldName) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value) ||
            int.TryParse(value, out _) ||
            !Enum.TryParse<TEnum>(value, true, out var parsed) ||
            !Enum.IsDefined(parsed))
            throw new ArgumentException($"'{value}' is not a valid {fieldName}.");

        return parsed;
    }

    public static EvaluationScenarioResource ToResource(EvaluationScenarioView view) =>
        new(
            view.ScenarioId,
            view.RequestId,
            view.Version,
            view.Status,
            view.CreatedBy,
            view.CreatedAt,
            view.SupersedesScenarioId,
            view.Criteria.Select(c => new EvaluationCriterionResource(
                c.CriterionId, c.Name, c.TargetField, c.Category, c.Mode, c.Operator, c.ExpectedValue, c.UnitOfMeasure, c.Weight, c.DisplayOrder)).ToList());
}
