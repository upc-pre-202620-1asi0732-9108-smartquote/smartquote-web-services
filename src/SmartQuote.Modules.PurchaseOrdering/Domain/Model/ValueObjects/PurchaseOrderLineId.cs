namespace SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

public record PurchaseOrderLineId
{
    public Guid Value { get; }

    public PurchaseOrderLineId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("PurchaseOrderLineId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(PurchaseOrderLineId id) => id.Value;

    public override string ToString() => Value.ToString();
}
