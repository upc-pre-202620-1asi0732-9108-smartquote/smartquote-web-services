using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using Xunit;

namespace SmartQuote.Domain.Tests;

public sealed class DeliveryEvaluationTests
{
    private static DeliveryEvaluation Create(int onTime = 4, int quality = 5, string? observations = null) =>
        DeliveryEvaluation.Create(
            new PurchaseOrderId(Guid.NewGuid()),
            "20698765432",
            onTime,
            quality,
            observations,
            new UserId(Guid.NewGuid()),
            DateTimeOffset.UtcNow);

    // US13/E1: Create Stores Scores Supplier And Trimmed Observations; comprobación aislada.
    [Fact]
    [Trait("Story", "US13"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public void CreateStoresScoresSupplierAndTrimmedObservations()
    {
        var evaluation = Create(onTime: 3, quality: 5, observations: "  Entrega tardía de un día.  ");

        Assert.Equal("20698765432", evaluation.SupplierTaxIdentifier);
        Assert.Equal(3, evaluation.OnTimeScore);
        Assert.Equal(5, evaluation.QualityScore);
        Assert.Equal("Entrega tardía de un día.", evaluation.Observations);
    }

    // US13/E2: Create Rejects Scores Outside The Allowed Scale; comprobación aislada.
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [Trait("Story", "US13"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public void CreateRejectsScoresOutsideTheAllowedScale(int score)
    {
        Assert.Throws<DomainException>(() => Create(onTime: score));
        Assert.Throws<DomainException>(() => Create(quality: score));
    }

    // US13/E2: Create Rejects Observations Longer Than The Limit; comprobación aislada.
    [Fact]
    [Trait("Story", "US13"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public void CreateRejectsObservationsLongerThanTheLimit()
    {
        var tooLong = new string('x', DeliveryEvaluation.MaximumObservationsLength + 1);

        Assert.Throws<DomainException>(() => Create(observations: tooLong));
    }

    // US13/E1: Create Accepts Empty Observations As Null; comprobación aislada.
    [Fact]
    [Trait("Story", "US13"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public void CreateAcceptsEmptyObservationsAsNull()
    {
        var evaluation = Create(observations: "   ");

        Assert.Null(evaluation.Observations);
    }

    // US13/E2: Create Accepts Exactly500Characters; comprobación aislada.
    [Fact]
    [Trait("Story", "US13"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public void CreateAcceptsExactly500Characters()
    {
        var observations = new string('x', 500);
        Assert.Equal(observations, Create(observations: observations).Observations);
    }
}
