namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record ExtractedFieldId
{
    public Guid Value { get; }

    public ExtractedFieldId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ExtractedFieldId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(ExtractedFieldId id) => id.Value;

    public override string ToString() => Value.ToString();
}
