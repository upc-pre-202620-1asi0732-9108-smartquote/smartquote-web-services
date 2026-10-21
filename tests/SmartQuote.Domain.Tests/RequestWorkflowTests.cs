using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.Entities;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;
using Xunit;

namespace SmartQuote.Domain.Tests;

public sealed class RequestWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);
    private static RequestedItem Item(string protein = "20")
    {
        var item = RequestedItem.Create("Alimento", 1000, "kg");
        item.AddRequirement(TechnicalRequirement.Create("Proteína", ComparisonOperator.GreaterThanOrEqual, protein, "%", true));
        return item;
    }
    private static RecurringRequestSchedule Schedule(int interval = 7, int offset = 2) => RecurringRequestSchedule.Create(Guid.NewGuid(),
        new(2026, 10, 20), interval, offset, "America/Lima", RequestPriority.Normal,
        [new("Alimento", 1000, "kg", [new("Proteína", ComparisonOperator.GreaterThanOrEqual, "20", "%", true)])], Now);

    // US15/E2: answering preserves interpretation without changing a mandatory requirement.
    [Fact, Trait("Story", "US15"), Trait("Scenario", "E2")]
    public void OnlyRecipientAnswersAndCannotAnswerTwice()
    {
        var specialist = Guid.NewGuid();
        var c = TechnicalClarification.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), specialist, "Interpretación?", 0, Now);
        Assert.Throws<UnauthorizedAccessException>(() => c.Respond("Respuesta", Guid.NewGuid(), Now));
        c.Respond("Proteína cruda", specialist, Now.AddMinutes(1));
        Assert.Equal("Answered", c.Status); Assert.Equal(specialist, c.AnsweredBy);
        Assert.Throws<DomainException>(() => c.Respond("Otra respuesta", specialist, Now));
        c.MarkRead(Now); c.MarkRead(Now.AddHours(1)); Assert.Equal(Now, c.NoticeReadAt);
    }

    // US15/E3: same requirement identity, immutable before/after evidence and mandatory protection.
    [Fact, Trait("Story", "US15"), Trait("Scenario", "E3")]
    public void RevisionPreservesIdentityAndMandatoryHistory()
    {
        var item = Item();
        var request = PurchaseRequest.Create(new(Guid.NewGuid()), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), RequestPriority.Normal, [item]);
        var id = item.Requirements[0].Id.Value;
        request.ReviseRequirement(Guid.NewGuid(), item.Id.Value, id,
            TechnicalRequirement.Create("Proteína", ComparisonOperator.GreaterThanOrEqual, "21", "%", true), request.RequesterId.Value, "Aclaración", Now);
        Assert.Equal(id, item.Requirements[0].Id.Value); Assert.Equal(1, request.TechnicalRevision);
        Assert.Equal("20", request.RequirementRevisions[0].Previous.ExpectedValue);
        Assert.Equal("21", request.RequirementRevisions[0].Current.ExpectedValue);
        Assert.Throws<DomainException>(() => request.ReviseRequirement(Guid.NewGuid(), item.Id.Value, id,
            TechnicalRequirement.Create("Proteína", ComparisonOperator.GreaterThanOrEqual, "21", "%", false), request.RequesterId.Value, "Remove", Now));
    }

    [Theory, Trait("Story", "US16"), Trait("Scenario", "E1")]
    [InlineData(0, 2), InlineData(-1, 2), InlineData(7, -1)]
    public void ScheduleRejectsInvalidPeriodOrOffset(int interval, int offset) => Assert.Throws<DomainException>(() => Schedule(interval, offset));

    [Fact, Trait("Story", "US16"), Trait("Scenario", "E1")]
    public void ScheduleKeepsZoneAndNextLocalMidnight()
    {
        var s = Schedule(); Assert.Equal("America/Lima", s.TimeZoneId);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 5, 0, 0, TimeSpan.Zero), s.NextOccurrenceAt);
        Assert.Empty(s.Occurrences);
    }

    [Fact, Trait("Story", "US16"), Trait("Scenario", "E2")]
    public void DraftHasNoSubmissionEventUntilSpecialistConfirms()
    {
        var request = PurchaseRequest.CreateDraft(new(Guid.NewGuid()), new(2026, 10, 22), RequestPriority.Normal, [Item()], Guid.NewGuid(), Now);
        Assert.Equal(RequestStatus.Draft, request.Status); Assert.Empty(request.DomainEvents);
        request.Submit(new(2026, 10, 20), Now); Assert.Equal(RequestStatus.Submitted, request.Status);
        Assert.Single(request.DomainEvents); Assert.Throws<DomainException>(() => request.Submit(new(2026, 10, 20), Now));
    }

    [Fact, Trait("Story", "US16"), Trait("Scenario", "E2")]
    public void ExpiredDraftCannotBeSubmitted()
    {
        var request = PurchaseRequest.CreateDraft(new(Guid.NewGuid()), new(2026, 10, 20), RequestPriority.Normal, [Item()], Guid.NewGuid(), Now);
        Assert.Throws<DomainException>(() => request.Submit(new(2026, 10, 21), Now.AddDays(1)));
        Assert.Equal(RequestStatus.Draft, request.Status);
    }

    [Fact, Trait("Story", "US16"), Trait("Scenario", "E4")]
    public void PausedSchedulePreservesPreviousOccurrences()
    {
        var s = Schedule(); s.RecordOccurrence(Guid.NewGuid(), Guid.NewGuid(), Now); s.Pause(Now);
        Assert.Single(s.Occurrences); Assert.False(s.IsActive); Assert.Equal(Now, s.PausedAt);
        Assert.Throws<DomainException>(() => s.RecordOccurrence(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(7)));
    }

    [Fact, Trait("Story", "US22"), Trait("Scenario", "E2")]
    public void ReassignmentPreservesAuthorRequesterAndRequiresReason()
    {
        var requester = new UserId(Guid.NewGuid()); var manager = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var request = PurchaseRequest.Create(requester, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), RequestPriority.Normal, [Item()]);
        request.AssignAnalyst(a, manager, null, Now);
        Assert.Throws<DomainException>(() => request.AssignAnalyst(b, manager, null, Now));
        request.AssignAnalyst(b, manager, "Redistribución", Now.AddMinutes(1));
        Assert.Equal(b, request.AssignedAnalystId); Assert.Equal(requester, request.RequesterId);
        Assert.Equal(RequestStatus.Submitted, request.Status); Assert.Equal(a, request.AssignmentHistory[1].PreviousAnalystId);
        Assert.Equal(manager, request.AssignmentHistory[1].AssignedBy);
    }
}
