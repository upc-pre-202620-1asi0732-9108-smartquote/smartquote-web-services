namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record QuotationLineId
{
    public Guid Value { get; }

    public QuotationLineId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("QuotationLineId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(QuotationLineId id) => id.Value;

    public override string ToString() => Value.ToString();
}
