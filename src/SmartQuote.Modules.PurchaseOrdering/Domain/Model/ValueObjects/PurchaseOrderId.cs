namespace SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

public record PurchaseOrderId
{
    public Guid Value { get; }

    public PurchaseOrderId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("PurchaseOrderId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(PurchaseOrderId id) => id.Value;

    public override string ToString() => Value.ToString();
}
