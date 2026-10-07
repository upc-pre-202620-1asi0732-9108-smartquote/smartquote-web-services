namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record SimulationRunId
{
    public Guid Value { get; }

    public SimulationRunId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("SimulationRunId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(SimulationRunId id) => id.Value;

    public override string ToString() => Value.ToString();
}
