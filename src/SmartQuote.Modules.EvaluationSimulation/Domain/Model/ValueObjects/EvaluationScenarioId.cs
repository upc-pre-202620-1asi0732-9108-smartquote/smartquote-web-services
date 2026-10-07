namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record EvaluationScenarioId
{
    public Guid Value { get; }

    public EvaluationScenarioId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("EvaluationScenarioId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(EvaluationScenarioId id) => id.Value;

    public override string ToString() => Value.ToString();
}
