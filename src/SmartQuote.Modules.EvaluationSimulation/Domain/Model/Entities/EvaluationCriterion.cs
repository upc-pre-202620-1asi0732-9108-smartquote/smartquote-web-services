using System.Globalization;
using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;

public class EvaluationCriterion
{
    public EvaluationCriterionId Id { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string TargetField { get; private set; } = string.Empty;
    public CriterionCategory Category { get; private set; }
    public CriterionMode Mode { get; private set; }
    public ComparisonOperator Operator { get; private set; }
    public string ExpectedValue { get; private set; } = string.Empty;
    public string UnitOfMeasure { get; private set; } = string.Empty;
    public decimal Weight { get; private set; }
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Whether this criterion compares an ordered numeric value, which is what makes
    /// relative min-max normalization across candidates meaningful (see <see cref="ResolveNumericValue"/>).
    /// </summary>
    public bool IsNumeric => Operator is ComparisonOperator.GreaterThanOrEqual or ComparisonOperator.LessThanOrEqual;

    /// <summary>True when a lower actual value is the better outcome (e.g. price, lead time).</summary>
    public bool LowerIsBetter => Operator == ComparisonOperator.LessThanOrEqual;

    private EvaluationCriterion() { }

    private EvaluationCriterion(
        EvaluationCriterionId id,
        string name,
        string targetField,
        CriterionCategory category,
        CriterionMode mode,
        ComparisonOperator @operator,
        string expectedValue,
        string unitOfMeasure,
        decimal weight,
        int displayOrder)
    {
        Id = id;
        Name = name;
        TargetField = targetField;
        Category = category;
        Mode = mode;
        Operator = @operator;
        ExpectedValue = expectedValue;
        UnitOfMeasure = unitOfMeasure;
        Weight = weight;
        DisplayOrder = displayOrder;
    }

    public static EvaluationCriterion Create(
        string name,
        string targetField,
        CriterionCategory category,
        CriterionMode mode,
        ComparisonOperator @operator,
        string expectedValue,
        string unitOfMeasure,
        decimal weight,
        int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Criterion name is required.");

        if (string.IsNullOrWhiteSpace(targetField))
            throw new DomainException("Criterion target field is required.");

        if (string.IsNullOrWhiteSpace(expectedValue))
            throw new DomainException("Criterion expected value is required.");

        if (!Enum.IsDefined(category) || !Enum.IsDefined(mode) || !Enum.IsDefined(@operator))
            throw new DomainException("Criterion category, mode, or comparison operator is invalid.");

        if (displayOrder <= 0)
            throw new DomainException("Criterion display order must be greater than zero.");

        if (category == CriterionCategory.Price && !targetField.Equals("totalPrice", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Price criteria must target 'totalPrice'.");

        if (category == CriterionCategory.DeliveryTime && !targetField.Equals("deliveryLeadTimeDays", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Delivery-time criteria must target 'deliveryLeadTimeDays'.");

        if (category == CriterionCategory.TechnicalCompliance && !Guid.TryParse(targetField, out _))
            throw new DomainException("Technical criteria must target a technical requirement UUID.");

        if (weight < 0)
            throw new DomainException("Criterion weight cannot be negative.");

        if (mode == CriterionMode.Mandatory && weight != 0)
            throw new DomainException("Mandatory criteria do not carry a weight.");

        return new EvaluationCriterion(
            new EvaluationCriterionId(Guid.NewGuid()), name, targetField, category, mode, @operator, expectedValue, unitOfMeasure, weight, displayOrder);
    }

    /// <summary>
    /// Self-contained pass/fail evaluation. Correct as-is for Mandatory criteria (US07: they only
    /// gate eligibility, no normalization applies). Weighted criteria use <see cref="ResolvePassed"/>,
    /// <see cref="ResolveNumericValue"/> and <see cref="BuildResult"/> instead, because normalizing them
    /// (docs/3-evaluation-simulation.puml step 3) requires comparing every eligible quotation's value
    /// for this same criterion, which a single quotation's input cannot provide on its own.
    /// </summary>
    public CriterionResult Evaluate(CriterionEvaluationInput input)
    {
        var actualValue = ResolveActualValue(input);
        var passed = ResolvePassed(input);
        var normalizedScore = passed ? 1m : 0m;

        return new CriterionResult(Id, passed, normalizedScore, 0m, BuildExplanation(actualValue, passed));
    }

    public bool ResolvePassed(CriterionEvaluationInput input)
    {
        var actualValue = ResolveActualValue(input);
        return actualValue is not null && CompareValues(actualValue, Operator, ExpectedValue);
    }

    /// <summary>Actual numeric value for this criterion, or null when it isn't comparable as a number.</summary>
    public decimal? ResolveNumericValue(CriterionEvaluationInput input) =>
        IsNumeric && TryParseDecimal(ResolveActualValue(input) ?? string.Empty, out var value) ? value : null;

    public CriterionResult BuildResult(CriterionEvaluationInput input, decimal normalizedScore)
    {
        var passed = ResolvePassed(input);
        var weightedContribution = normalizedScore * (Weight / 100m);

        return new CriterionResult(Id, passed, normalizedScore, weightedContribution, BuildExplanation(ResolveActualValue(input), passed));
    }

    private string BuildExplanation(string? actualValue, bool passed) => passed
        ? $"{Name}: '{actualValue}' satisfies {Operator} '{ExpectedValue}{UnitOfMeasure}'."
        : $"{Name}: '{actualValue ?? "N/A"}' does not satisfy {Operator} '{ExpectedValue}{UnitOfMeasure}'.";

    private string? ResolveActualValue(CriterionEvaluationInput input) => Category switch
    {
        CriterionCategory.Price => input.TotalPrice.ToString(CultureInfo.InvariantCulture),
        CriterionCategory.DeliveryTime => input.DeliveryLeadTimeDays.ToString(CultureInfo.InvariantCulture),
        CriterionCategory.TechnicalCompliance => input.TechnicalValuesByRequirementId.TryGetValue(TargetField, out var specification)
            ? specification.Value
            : null,
        _ => null
    };

    private static bool CompareValues(string actual, ComparisonOperator op, string expected) => op switch
    {
        ComparisonOperator.Equals => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
        ComparisonOperator.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
        ComparisonOperator.GreaterThanOrEqual => TryParseDecimal(actual, out var a1) && TryParseDecimal(expected, out var e1) && a1 >= e1,
        ComparisonOperator.LessThanOrEqual => TryParseDecimal(actual, out var a2) && TryParseDecimal(expected, out var e2) && a2 <= e2,
        _ => false
    };

    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
}
