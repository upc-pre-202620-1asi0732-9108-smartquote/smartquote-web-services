using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

public record OrderNumber
{
    public string Value { get; }

    public OrderNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Order number is required.");

        Value = value;
    }

    public override string ToString() => Value;
}
