using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Domain.Model.Entities;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;

public sealed record ScheduleRequirement(string Name, ComparisonOperator Operator, string ExpectedValue, string UnitOfMeasure, bool IsMandatory);
public sealed record ScheduleItem(string Description, decimal Quantity, string UnitOfMeasure, IReadOnlyList<ScheduleRequirement> Requirements)
{
    public RequestedItem BuildItem()
    {
        var item = RequestedItem.Create(Description, Quantity, UnitOfMeasure);
        foreach (var r in Requirements) item.AddRequirement(TechnicalRequirement.Create(r.Name, r.Operator, r.ExpectedValue, r.UnitOfMeasure, r.IsMandatory));
        if (!item.HasMandatoryRequirement()) throw new DomainException("Each scheduled item needs a mandatory requirement.");
        return item;
    }
}

public sealed record ScheduleOccurrence(Guid Id, DateOnly ScheduledDate, Guid? RequestId, string Status, DateTimeOffset ProcessedAt);

public sealed class RecurringRequestSchedule
{
    private readonly List<ScheduleOccurrence> _occurrences = [];
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public int IntervalDays { get; private set; }
    public int RequiredDateOffsetDays { get; private set; }
    public string TimeZoneId { get; private set; } = "";
    public RequestPriority Priority { get; private set; }
    public IReadOnlyList<ScheduleItem> Items { get; private set; } = [];
    public bool IsActive { get; private set; }
    public DateOnly NextOccurrenceDate { get; private set; }
    public DateTimeOffset NextOccurrenceAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PausedAt { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyList<ScheduleOccurrence> Occurrences => _occurrences.AsReadOnly();
    private RecurringRequestSchedule() { }

    public static RecurringRequestSchedule Create(Guid owner, DateOnly start, int intervalDays, int offsetDays,
        string timeZoneId, RequestPriority priority, IReadOnlyList<ScheduleItem> items, DateTimeOffset now)
    {
        if (intervalDays is <= 0 or > 3650 || offsetDays is < 0 or > 3650)
            throw new DomainException("Interval must be 1–3650 days and required-date offset 0–3650 days.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { throw new DomainException("The operation time zone is invalid."); }
        if (start < DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime))
            throw new DomainException("Schedule start date cannot be in the past.");
        if (!Enum.IsDefined(priority) || items.Count == 0 || items.Count > 100)
            throw new DomainException("A schedule needs a valid priority and 1–100 items.");
        foreach (var item in items) item.BuildItem();
        var schedule = new RecurringRequestSchedule { Id = Guid.NewGuid(), OwnerId = owner, StartDate = start,
            IntervalDays = intervalDays, RequiredDateOffsetDays = offsetDays, TimeZoneId = timeZoneId,
            Priority = priority, Items = items, IsActive = true, CreatedAt = now, Version = 1 };
        schedule.SetNext(start);
        return schedule;
    }

    public void RecordOccurrence(Guid occurrenceId, Guid? requestId, DateTimeOffset now)
    {
        if (!IsActive || now < NextOccurrenceAt) throw new DomainException("This schedule is not due.");
        if (_occurrences.Any(o => o.ScheduledDate == NextOccurrenceDate)) throw new ConflictException("This occurrence was already processed.");
        _occurrences.Add(new(occurrenceId, NextOccurrenceDate, requestId, requestId.HasValue ? "Generated" : "SkippedExpired", now));
        SetNext(NextOccurrenceDate.AddDays(IntervalDays)); Version++;
    }

    public void Pause(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false; PausedAt = now; Version++;
    }

    private void SetNext(DateOnly date)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // A timezone transition can skip midnight. Choose the first valid local time that day.
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        NextOccurrenceDate = date;
        NextOccurrenceAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
}
