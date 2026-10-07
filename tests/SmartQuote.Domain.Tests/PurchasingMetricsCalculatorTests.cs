using SmartQuote.API.Analytics;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using Xunit;

namespace SmartQuote.Domain.Tests;

public sealed class PurchasingMetricsCalculatorTests
{
    // US14/E1: Selected Cheapest Quotation Uses Cheapest Other Eligible Offer; comprobación aislada.
    [Fact]
    [Trait("Story", "US14"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public void SelectedCheapestQuotationUsesCheapestOtherEligibleOffer()
    {
        var selected = Guid.NewGuid().ToString();
        var alternative = Guid.NewGuid().ToString();
        var excluded = Guid.NewGuid().ToString();
        var run = Run((selected, "PEN", 80m), (alternative, "PEN", 100m), (excluded, "PEN", 200m));
        run.AddEvaluation(QuotationEvaluation.Create(selected));
        run.AddEvaluation(QuotationEvaluation.Create(alternative));
        var ineligible = QuotationEvaluation.Create(excluded);
        ineligible.Exclude(new ExclusionReason(new EvaluationCriterionId(Guid.NewGuid()),
            "MANDATORY_REQUIREMENT", "Mandatory requirement not met"));
        run.AddEvaluation(ineligible);

        Assert.Equal(20m, PurchasingMetricsCalculator.ComparativeSavingsPen(run, selected));
        Assert.Equal(0m, PurchasingMetricsCalculator.ComparativeSavingsPen(run, alternative));
    }

    // US14/E1: Mixed Currencies Use Stored Rate; comprobación aislada.
    [Fact]
    [Trait("Story", "US14"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public void MixedCurrenciesUseStoredRate()
    {
        var selected = Guid.NewGuid().ToString();
        var alternative = Guid.NewGuid().ToString();
        var rate = new ExchangeRateSnapshot("USD", "PEN", 4m, "SELL", new DateOnly(2026, 10, 1),
            "Test rate", DateTimeOffset.UtcNow);
        var run = Run(rate, (selected, "USD", 20m), (alternative, "PEN", 100m));
        run.AddEvaluation(QuotationEvaluation.Create(selected));
        run.AddEvaluation(QuotationEvaluation.Create(alternative));

        Assert.Equal(20m, PurchasingMetricsCalculator.ComparativeSavingsPen(run, selected));
    }

    // US14/E3: Missing Comparable Alternative Is Unavailable; comprobación aislada.
    [Fact]
    [Trait("Story", "US14"), Trait("Scenario", "E3"), Trait("Category", "Unit")]
    public void MissingComparableAlternativeIsUnavailable()
    {
        var selected = Guid.NewGuid().ToString();
        var alternative = Guid.NewGuid().ToString();
        var run = Run((selected, "USD", 20m), (alternative, "PEN", 100m));
        run.AddEvaluation(QuotationEvaluation.Create(selected));
        run.AddEvaluation(QuotationEvaluation.Create(alternative));

        Assert.Null(PurchasingMetricsCalculator.ComparativeSavingsPen(run, selected));
        Assert.Null(PurchasingMetricsCalculator.ComparativeSavingsPen(run, Guid.NewGuid().ToString()));
    }

    private static SimulationRun Run(params (string Id, string Currency, decimal Price)[] quotations) =>
        Run(null, quotations);

    private static SimulationRun Run(ExchangeRateSnapshot? rate,
        params (string Id, string Currency, decimal Price)[] quotations) =>
        SimulationRun.Create(new EvaluationScenarioId(Guid.NewGuid()), 1, InputFingerprint.FromParts("test"),
            new RequestEvaluationSnapshot(Guid.NewGuid().ToString(), 1, DateOnly.FromDateTime(DateTime.UtcNow),
                "High", [], DateTimeOffset.UtcNow),
            quotations.Select((quotation, index) =>
                new QuotationEvaluationSnapshot(quotation.Id, 1, "supplier", "Supplier", "12345678901",
                    quotation.Currency, 1, DateTimeOffset.UtcNow,
                    [new QuotationLineSnapshotData(null, null, index + 1, "Supply", 1m, "unit",
                        quotation.Price, [])], DateTimeOffset.UtcNow)).ToArray(), rate);
}
