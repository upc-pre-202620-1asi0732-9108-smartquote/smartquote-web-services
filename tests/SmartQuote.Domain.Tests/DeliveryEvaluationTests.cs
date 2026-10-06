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

    [Fact]
    public void CreateStoresScoresSupplierAndTrimmedObservations()
    {
        var evaluation = Create(onTime: 3, quality: 5, observations: "  Entrega tardía de un día.  ");

        Assert.Equal("20698765432", evaluation.SupplierTaxIdentifier);
        Assert.Equal(3, evaluation.OnTimeScore);
        Assert.Equal(5, evaluation.QualityScore);
        Assert.Equal("Entrega tardía de un día.", evaluation.Observations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void CreateRejectsScoresOutsideTheAllowedScale(int score)
    {
        Assert.Throws<DomainException>(() => Create(onTime: score));
        Assert.Throws<DomainException>(() => Create(quality: score));
    }

    [Fact]
    public void CreateRejectsObservationsLongerThanTheLimit()
    {
        var tooLong = new string('x', DeliveryEvaluation.MaximumObservationsLength + 1);

        Assert.Throws<DomainException>(() => Create(observations: tooLong));
    }

    [Fact]
    public void CreateAcceptsEmptyObservationsAsNull()
    {
        var evaluation = Create(observations: "   ");

        Assert.Null(evaluation.Observations);
    }

    [Fact]
    public void CreateAcceptsExactly500Characters()
    {
        var observations = new string('x', 500);
        Assert.Equal(observations, Create(observations: observations).Observations);
    }
}
