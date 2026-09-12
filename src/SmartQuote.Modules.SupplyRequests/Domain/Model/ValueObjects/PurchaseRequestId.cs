namespace SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

public record PurchaseRequestId
{
    public Guid Value { get; }

    public PurchaseRequestId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("PurchaseRequestId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(PurchaseRequestId id) => id.Value;

    public override string ToString() => Value.ToString();
}
