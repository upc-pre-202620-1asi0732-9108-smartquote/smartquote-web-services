using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using System.Globalization;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

/// <summary>
/// Immutable evidence of the official exchange rate used by a simulation.
/// </summary>
public class ExchangeRateSnapshot
{
    public string SourceCurrency { get; private set; } = string.Empty;
    public string TargetCurrency { get; private set; } = string.Empty;
    public decimal Rate { get; private set; }
    public string RateType { get; private set; } = string.Empty;
    public DateOnly PublishedOn { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public DateTimeOffset RetrievedAt { get; private set; }

    private ExchangeRateSnapshot() { }

    public ExchangeRateSnapshot(
        string sourceCurrency,
        string targetCurrency,
        decimal rate,
        string rateType,
        DateOnly publishedOn,
        string source,
        DateTimeOffset retrievedAt)
    {
        if (string.IsNullOrWhiteSpace(sourceCurrency) || sourceCurrency.Length != 3)
            throw new DomainException("Exchange-rate source currency must be a 3-letter ISO code.");
        if (string.IsNullOrWhiteSpace(targetCurrency) || targetCurrency.Length != 3)
            throw new DomainException("Exchange-rate target currency must be a 3-letter ISO code.");
        if (rate <= 0)
            throw new DomainException("Exchange rate must be greater than zero.");
        if (string.IsNullOrWhiteSpace(rateType))
            throw new DomainException("Exchange-rate type is required.");
        if (string.IsNullOrWhiteSpace(source))
            throw new DomainException("Exchange-rate source is required.");

        SourceCurrency = sourceCurrency.Trim().ToUpperInvariant();
        TargetCurrency = targetCurrency.Trim().ToUpperInvariant();
        Rate = rate;
        RateType = rateType.Trim().ToUpperInvariant();
        PublishedOn = publishedOn;
        Source = source.Trim();
        RetrievedAt = retrievedAt;
    }

    public Money Convert(Money amount)
    {
        if (amount.Currency == TargetCurrency)
            return amount;
        if (amount.Currency != SourceCurrency)
            throw new DomainException($"Currency '{amount.Currency}' cannot be converted with the stored {SourceCurrency}/{TargetCurrency} rate.");

        return new Money(decimal.Round(amount.Amount * Rate, 4, MidpointRounding.AwayFromZero), TargetCurrency);
    }

    public string FingerprintPart() =>
        $"{SourceCurrency}:{TargetCurrency}:{Rate.ToString("G29", CultureInfo.InvariantCulture)}:{RateType}:{PublishedOn:yyyy-MM-dd}:{Source}";
}
