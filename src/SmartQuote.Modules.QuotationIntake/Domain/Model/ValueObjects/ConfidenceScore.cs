using SmartQuote.API.Shared.Domain;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record ConfidenceScore
{
    public decimal Value { get; }

    public ConfidenceScore(decimal value)
    {
        if (value is < 0 or > 1)
            throw new DomainException("Confidence must be between 0 and 1.");

        Value = value;
    }

    public bool IsAbove(decimal threshold) => Value > threshold;
}
