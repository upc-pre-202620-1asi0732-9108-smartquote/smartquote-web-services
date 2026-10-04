using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.Entities;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;
using Xunit;

namespace SmartQuote.Domain.Tests;

public sealed class PurchaseRequestTests
{
    [Fact]
    public void CreateRequiresAtLeastOneItemWithAMandatoryRequirement()
    {
        var requester = new UserId(Guid.NewGuid());
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        Assert.Throws<DomainException>(() =>
            PurchaseRequest.Create(requester, date, RequestPriority.Normal, []));

        var item = RequestedItem.Create("Feed", 10, "kg");
        item.AddRequirement(TechnicalRequirement.Create(
            "Protein", ComparisonOperator.GreaterThanOrEqual, "20", "%", false));
        Assert.Throws<DomainException>(() =>
            PurchaseRequest.Create(requester, date, RequestPriority.Normal, [item]));
    }

    [Fact]
    public void CreateStartsSubmittedWithInitialHistoryAndVersion()
    {
        var request = NewRequest();

        Assert.Equal(RequestStatus.Submitted, request.Status);
        Assert.Equal(1, request.Version);
        var entry = Assert.Single(request.StatusHistory);
        Assert.Equal(RequestStatus.Draft, entry.FromStatus);
        Assert.Equal(RequestStatus.Submitted, entry.ToStatus);
    }

    [Fact]
    public void AllowedTransitionsAdvanceVersionAndKeepHistory()
    {
        var request = NewRequest();
        var analyst = new UserId(Guid.NewGuid());

        request.ChangeStatus(RequestStatus.UnderReview, analyst, "Review started");
        request.ChangeStatus(RequestStatus.QuotationCollection, analyst, "Request is complete");

        Assert.Equal(RequestStatus.QuotationCollection, request.Status);
        Assert.Equal(3, request.Version);
        Assert.Collection(request.StatusHistory,
            entry => Assert.Equal(RequestStatus.Submitted, entry.ToStatus),
            entry => Assert.Equal(RequestStatus.UnderReview, entry.ToStatus),
            entry => Assert.Equal(RequestStatus.QuotationCollection, entry.ToStatus));
    }

    [Fact]
    public void InvalidTransitionCannotSkipReview()
    {
        var request = NewRequest();

        Assert.Throws<DomainException>(() => request.ChangeStatus(
            RequestStatus.Approved, new UserId(Guid.NewGuid()), "Skip review"));

        Assert.Equal(RequestStatus.Submitted, request.Status);
        Assert.Equal(1, request.Version);
        Assert.Single(request.StatusHistory);
    }

    [Fact]
    public void StatusChangeRequiresReasonAndDifferentNextStatus()
    {
        var request = NewRequest();
        var analyst = new UserId(Guid.NewGuid());

        Assert.Throws<DomainException>(() => request.ChangeStatus(
            RequestStatus.Submitted, analyst, "Repeated"));
        Assert.Throws<DomainException>(() => request.ChangeStatus(
            RequestStatus.UnderReview, analyst, " "));

        Assert.Equal(RequestStatus.Submitted, request.Status);
        Assert.Equal(1, request.Version);
        Assert.Single(request.StatusHistory);
    }

    [Fact]
    public void CancelledRequestCannotReopen()
    {
        var request = NewRequest();
        var analyst = new UserId(Guid.NewGuid());
        request.ChangeStatus(RequestStatus.Cancelled, analyst, "No longer needed");

        Assert.Throws<DomainException>(() => request.ChangeStatus(
            RequestStatus.UnderReview, analyst, "Reopen"));

        Assert.Equal(RequestStatus.Cancelled, request.Status);
        Assert.Equal(2, request.Version);
        Assert.Equal(2, request.StatusHistory.Count);
    }

    private static PurchaseRequest NewRequest()
    {
        var item = RequestedItem.Create("Feed", 10, "kg");
        item.AddRequirement(TechnicalRequirement.Create(
            "Protein", ComparisonOperator.GreaterThanOrEqual, "20", "%", true));
        return PurchaseRequest.Create(
            new UserId(Guid.NewGuid()),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            RequestPriority.Normal,
            [item]);
    }
}
