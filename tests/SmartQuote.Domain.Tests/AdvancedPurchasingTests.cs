using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Services;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;
using Xunit;

namespace SmartQuote.Domain.Tests;

[Trait("Category", "Unit")]
public class AdvancedPurchasingTests
{
    // US17/E1-E2: same preventive alert preserves acknowledgement across reassignment.
    [Fact, Trait("Story", "US17"), Trait("Scenario", "E2")]
    public void AlertReadIsIdempotentAndReassignmentKeepsHistory()
    {
        var now = DateTimeOffset.UtcNow; var today = DateOnly.FromDateTime(now.DateTime); var owner = Guid.NewGuid();
        var alert = QuotationExpiryAlert.Create(Guid.NewGuid(), Guid.NewGuid(), owner, "Supplier", today.AddDays(2), 3, today, now);
        alert.Read(owner, now); alert.Read(owner, now.AddHours(1)); Assert.Equal(now, alert.ReadAt);
        alert.Transfer(Guid.NewGuid(), now.AddHours(2)); Assert.Equal(now, alert.ReadAt); Assert.Equal(now.AddHours(2), alert.ReassignedAt);
        Assert.Throws<UnauthorizedAccessException>(() => alert.Read(owner, now));
    }
    [Fact, Trait("Story", "US17"), Trait("Scenario", "E3")]
    public void ExpiredOfferCannotCreatePreventiveAlert()
    {
        var today = new DateOnly(2026, 10, 20);
        Assert.Throws<DomainException>(() => QuotationExpiryAlert.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Supplier", today.AddDays(-1), 3, today, DateTimeOffset.UtcNow));
    }
    private static (EvaluationScenario Scenario, EvaluationDataset Dataset) Fixture(bool insufficient = false)
    {
        var scenario = EvaluationScenario.Create(Guid.NewGuid().ToString(), new UserId(Guid.NewGuid()));
        var items = Enumerable.Range(0, 2).Select(i => new RequestItemSnapshotData(Guid.NewGuid().ToString(), i + 1, "Item " + i, 10, "kg",
            [new(Guid.NewGuid().ToString(), "Proteína mínima", ComparisonOperator.GreaterThanOrEqual, "20", "%", true)])).ToList();
        foreach (var item in items) scenario.AddCriterion(EvaluationCriterion.Create("Requirement " + item.LineNumber, item.Requirements[0].SourceRequirementId!,
            CriterionCategory.TechnicalCompliance, CriterionMode.Mandatory, ComparisonOperator.GreaterThanOrEqual, "20", "%", 0, item.LineNumber));
        scenario.AddCriterion(EvaluationCriterion.Create("Price", "totalPrice", CriterionCategory.Price, CriterionMode.Weighted, ComparisonOperator.LessThanOrEqual, "999999", "PEN", 100, 3)); scenario.Activate();
        var request = new RequestEvaluationSnapshot(scenario.RequestId, 1, new(2026, 12, 31), "Normal", items, DateTimeOffset.UtcNow);
        var quotes = Enumerable.Range(0, 2).Select(s => new QuotationEvaluationSnapshot(Guid.NewGuid().ToString(), 1, Guid.NewGuid().ToString(), "Supplier " + s,
            "2012345678" + s, s == 0 ? "USD" : "PEN", 3, DateTimeOffset.UtcNow,
            items.Select((item, i) => new QuotationLineSnapshotData(Guid.NewGuid().ToString(), item.SourceRequestedItemId, i + 1, item.Description,
                insufficient && i == 1 ? 5 : 20, "kg", s == 0 ? (i == 0 ? 1m : 4m) : (i == 0 ? 5m : 2m), [new("Proteína cruda", "21", "%")])).ToList(),
            DateTimeOffset.UtcNow, Guid.NewGuid(), "Enabled", 2, new(2026, 12, 31))).ToList();
        return (scenario, new(request, quotes));
    }
    [Fact, Trait("Story", "US21"), Trait("Scenario", "E1")]
    public void DistributedRankingUsesRequestedQuantitiesAndSameOfficialRate()
    {
        var (scenario, data) = Fixture(); var rate = new ExchangeRateSnapshot("USD", "PEN", 3.5m, "V", new(2026, 10, 20), "SUNAT", DateTimeOffset.UtcNow);
        var result = new DistributedSimulationEngine(new()).Evaluate(scenario, data, rate, new(2026, 10, 20));
        Assert.True(result.CanApprove); Assert.Equal(55m, result.TotalPen); Assert.Equal(2, result.Allocations.Select(a => a.SupplierAccountId).Distinct().Count());
        Assert.All(result.Allocations, a => Assert.Equal(10, a.Quantity)); Assert.Contains(result.Allocations, a => a.OriginalCurrency == "USD" && a.AmountPen == 35);
    }
    [Fact, Trait("Story", "US21"), Trait("Scenario", "E2")]
    public void InsufficientOffersAreNotAddedTogetherToCoverAnItem()
    {
        var (scenario, data) = Fixture(true); var result = new DistributedSimulationEngine(new()).Evaluate(scenario, data,
            new("USD", "PEN", 3.5m, "V", new(2026, 10, 20), "SUNAT", DateTimeOffset.UtcNow), new(2026, 10, 20));
        Assert.False(result.CanApprove); Assert.Single(result.UncoveredItemIds); Assert.Single(result.Allocations);
    }
    [Fact, Trait("Story", "US19"), Trait("Scenario", "E3")]
    public void SensitivityCannotPersistAConclusionWithOnlyOneEligibleOffer()
    {
        var (scenario, data) = Fixture();
        var config = new SensitivityConfiguration(Guid.NewGuid(), [], [new("one", true, 100, []), new("two", false, 0, ["Excluded"])], ["one"], "one");
        Assert.Throws<DomainException>(() => SensitivityAnalysis.Create(scenario, data, null, [config, config], Guid.NewGuid(), DateTimeOffset.UtcNow));
    }
}
