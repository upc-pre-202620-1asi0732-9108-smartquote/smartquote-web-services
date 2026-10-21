using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;
using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.PurchaseOrdering.Application;
using SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;
using Xunit;

namespace SmartQuote.Domain.Tests;

public sealed class OrderingPolicyTests
{
    [Fact, Trait("Story", "US18"), Trait("Scenario", "E1")]
    public void IdentifiedSupplierStartsPendingAndDecisionPreservesEvidence()
    {
        var now = DateTimeOffset.UtcNow; var manager = Guid.NewGuid();
        var supplier = SupplierAccount.Register(" 20123456789 ", "Proveedor", now);
        Assert.Equal("Pending", supplier.Status); Assert.Equal("20123456789", supplier.TaxIdentifier);
        Assert.Throws<DomainException>(() => supplier.Decide("enable", " ", manager, now));
        supplier.Decide("enable", "Evaluación interna completada", manager, now);
        var entry = Assert.Single(supplier.History); Assert.Equal("Pending", entry.PreviousStatus);
        Assert.Equal("Enabled", entry.NewStatus); Assert.Equal(manager, entry.DecidedBy); Assert.Equal(2, supplier.Version);
    }
    [Fact, Trait("Story", "US18"), Trait("Scenario", "E4")]
    public void ReactivationIncrementsVersionEvenThoughStatusReturnsToEnabled()
    {
        var supplier = SupplierAccount.Register("20123456789", "Proveedor", DateTimeOffset.UtcNow);
        var actor = Guid.NewGuid();
        supplier.Decide("enable", "Autorizado", actor, DateTimeOffset.UtcNow);
        var enabledVersion = supplier.Version;
        supplier.Decide("suspend", "Incidente de calidad", actor, DateTimeOffset.UtcNow);
        supplier.Decide("reactivate", "Incidente resuelto", actor, DateTimeOffset.UtcNow);
        Assert.Equal("Enabled", supplier.Status); Assert.True(supplier.Version > enabledVersion); Assert.Equal(3, supplier.History.Count);
    }
    [Theory, Trait("Story", "US20"), Trait("Scenario", "E1")]
    [InlineData(0, "PEN"), InlineData(-1, "PEN"), InlineData(100, "USD")]
    public void InvalidBudgetCannotBeAuthorized(decimal amount, string currency) => Assert.Throws<DomainException>(() =>
        PurchaseBudget.Create(Guid.NewGuid(), amount, currency, "Autorización", Guid.NewGuid(), DateTimeOffset.UtcNow));

    [Fact, Trait("Story", "US20"), Trait("Scenario", "E1")]
    public void MissingBudgetIsNotZero()
    {
        var result = BudgetAssessment.Evaluate(null, null, [4000m]);
        Assert.Equal("NotConfigured", result.Status); Assert.Null(result.BudgetAmountPen); Assert.Null(result.MarginPen);
    }
    [Fact, Trait("Story", "US20"), Trait("Scenario", "E2"), Trait("Scenario", "E3")]
    public void BudgetChecksWholeAcquisitionNotIndividualSupplierAmounts()
    {
        Assert.True(BudgetAssessment.Evaluate(1, 1000, [600m, 400m]).CanApprove);
        var exceeded = BudgetAssessment.Evaluate(1, 1000, [600m, 600m]);
        Assert.False(exceeded.CanApprove); Assert.Equal(1200, exceeded.TotalPen); Assert.Equal(200, exceeded.ExcessPen);
    }
    [Fact, Trait("Story", "US20"), Trait("Scenario", "E3")]
    public void UsdRequiresPreservedRateAndCannotOmitLinesToMatchBudget()
    {
        var snapshot = new ApprovedSimulationSnapshot(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(), "Proveedor", "20123456789", "USD", [new(null, null, "Alimento", 1000, "kg", 1)], 3, "hash", true,
            null, 3500);
        Assert.Throws<DomainException>(() => OrderingPolicyService.Assess(snapshot, null));
        var rate = new ApprovedExchangeRateSnapshot("USD", "PEN", 3.5m, "V", new(2026, 10, 20), "SUNAT", DateTimeOffset.UtcNow);
        Assert.Equal(3500, OrderingPolicyService.Assess(snapshot with { ExchangeRate = rate }, null).TotalPen);
        Assert.Throws<DomainException>(() => OrderingPolicyService.Assess(snapshot with
            { ExchangeRate = rate, OrderLines = [new(null, null, "Alimento", 500, "kg", 1)] }, null));
    }
    [Fact, Trait("Story", "US20"), Trait("Scenario", "E4")]
    public void BudgetRevisionKeepsBothAuthorizations()
    {
        var manager = Guid.NewGuid();
        var budget = PurchaseBudget.Create(Guid.NewGuid(), 5000, "PEN", "Límite inicial", manager, DateTimeOffset.UtcNow);
        budget.Revise(3500, "PEN", "Nuevo límite", manager, DateTimeOffset.UtcNow);
        Assert.Equal(2, budget.Version); Assert.Equal(5000, budget.Revisions[0].AmountPen); Assert.Equal(3500, budget.Revisions[1].AmountPen);
    }
}
