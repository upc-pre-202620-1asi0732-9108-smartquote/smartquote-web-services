namespace SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

public record DeliveryEvaluationId
{
    public Guid Value { get; }

    public DeliveryEvaluationId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("DeliveryEvaluationId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(DeliveryEvaluationId id) => id.Value;

    public override string ToString() => Value.ToString();
}
