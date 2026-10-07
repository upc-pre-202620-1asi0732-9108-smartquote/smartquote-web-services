namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record PoultryQuoteId
{
    public Guid Value { get; }

    public PoultryQuoteId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("PoultryQuoteId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(PoultryQuoteId id) => id.Value;

    public override string ToString() => Value.ToString();
}
