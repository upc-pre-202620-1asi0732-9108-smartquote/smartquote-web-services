namespace SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

public record RequestedItemId
{
    public Guid Value { get; }

    public RequestedItemId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("RequestedItemId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(RequestedItemId id) => id.Value;

    public override string ToString() => Value.ToString();
}
