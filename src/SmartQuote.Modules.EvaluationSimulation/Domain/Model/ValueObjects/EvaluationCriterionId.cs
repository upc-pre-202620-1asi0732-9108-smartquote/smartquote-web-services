namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record EvaluationCriterionId
{
    public Guid Value { get; }

    public EvaluationCriterionId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("EvaluationCriterionId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(EvaluationCriterionId id) => id.Value;

    public override string ToString() => Value.ToString();
}
