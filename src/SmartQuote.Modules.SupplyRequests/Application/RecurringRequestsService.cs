using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.Commands;

namespace SmartQuote.API.SupplyRequests.Application;

public sealed class RecurringRequestsService(ISupplyWorkflowStore store, IPurchaseRequestRepository requests,
    ISupplyRequestsUnitOfWork uow, IRequestUserDirectory users, ICurrentUser actor, TimeProvider clock,
    IDomainEventDispatcher events)
{
    private async Task RequireOwnerRole(CancellationToken ct)
    {
        if (!actor.IsInRole(SmartQuoteRoles.ProductionSpecialist) ||
            !await users.IsActiveInRoleAsync(actor.UserId, SmartQuoteRoles.ProductionSpecialist, ct))
            throw new UnauthorizedAccessException("An active production specialist account is required.");
    }
    public async Task<RecurringRequestSchedule> CreateAsync(DateOnly startDate, int intervalDays, int offsetDays,
        string timeZoneId, RegisterPurchaseRequestCommand template, CancellationToken ct)
    {
        await RequireOwnerRole(ct);
        var items = template.Items.Select(i => new ScheduleItem(i.Description, i.Quantity, i.UnitOfMeasure,
            i.Requirements.Select(r => new ScheduleRequirement(r.Name, r.Operator, r.ExpectedValue, r.UnitOfMeasure, r.IsMandatory)).ToList())).ToList();
        var schedule = RecurringRequestSchedule.Create(actor.UserId, startDate, intervalDays, offsetDays,
            timeZoneId, template.Priority, items, clock.GetUtcNow());
        store.AddSchedule(schedule);
        await uow.CompleteAsync(ct);
        return schedule;
    }
    public async Task<IReadOnlyList<RecurringRequestSchedule>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        await RequireOwnerRole(ct);
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Invalid pagination.");
        return await store.ListSchedulesAsync(actor.UserId, page, pageSize, ct);
    }
    public async Task<RecurringRequestSchedule> GetAsync(Guid id, CancellationToken ct)
    {
        await RequireOwnerRole(ct);
        var schedule = await store.GetScheduleAsync(id, ct) ?? throw new NotFoundException("Schedule was not found.");
        if (schedule.OwnerId != actor.UserId) throw new UnauthorizedAccessException();
        return schedule;
    }
    public async Task<RecurringRequestSchedule> PauseAsync(Guid id, long version, CancellationToken ct)
    {
        await RequireOwnerRole(ct);
        await using var lease = await store.LockScheduleAsync(id, ct);
        if (lease.Schedule.OwnerId != actor.UserId) throw new UnauthorizedAccessException();
        RequestCollaborationService.CheckVersion(lease.Schedule.Version, version);
        lease.Schedule.Pause(clock.GetUtcNow());
        await lease.CommitAsync(ct);
        return lease.Schedule;
    }
    public async Task EditDraftAsync(Guid id, RegisterPurchaseRequestCommand command, long version, CancellationToken ct)
    {
        await RequireOwnerRole(ct);
        var request = await OwnedRequest(id, ct);
        RequestCollaborationService.CheckVersion(request.Version, version);
        request.UpdateDraft(command.RequiredDate, command.Priority, command.Items.Select(PurchaseRequestCommandService.BuildRequestedItem), clock.GetUtcNow());
        await uow.CompleteAsync(ct);
    }
    public async Task SubmitAsync(Guid id, long version, CancellationToken ct)
    {
        await RequireOwnerRole(ct);
        var request = await OwnedRequest(id, ct);
        RequestCollaborationService.CheckVersion(request.Version, version);
        var timeZone = "America/Lima";
        // Recover the originating schedule without requiring its ongoing activation.
        if (request.ScheduleOccurrenceId.HasValue)
        {
            var schedule = await store.GetScheduleForOccurrenceAsync(request.ScheduleOccurrenceId.Value, ct);
            timeZone = schedule?.TimeZoneId ?? timeZone;
        }
        var now = clock.GetUtcNow();
        request.Submit(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime), now);
        await uow.CompleteAsync(ct);
        await events.DispatchAsync(request.DomainEvents, ct);
        request.ClearDomainEvents();
    }
    private async Task<PurchaseRequest> OwnedRequest(Guid id, CancellationToken ct)
    {
        var request = await requests.GetByIdAsync(new(id), ct) ?? throw new NotFoundException("Request was not found.");
        if (request.RequesterId.Value != actor.UserId) throw new UnauthorizedAccessException();
        return request;
    }
}

// System use case: no HTTP identity and no automatic submission to purchasing.
public sealed class RecurringOccurrenceProcessor(ISupplyWorkflowStore store, IPurchaseRequestRepository requests, TimeProvider clock)
{
    public async Task<int> ProcessAsync(Guid scheduleId, int maxOccurrences, CancellationToken ct = default)
    {
        if (maxOccurrences is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maxOccurrences));
        await using var lease = await store.LockScheduleAsync(scheduleId, ct);
        var schedule = lease.Schedule;
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId)).DateTime);
        var processed = 0;
        while (schedule.IsActive && schedule.NextOccurrenceAt <= now && processed < maxOccurrences)
        {
            var occurrenceId = Guid.NewGuid();
            var required = schedule.NextOccurrenceDate.AddDays(schedule.RequiredDateOffsetDays);
            PurchaseRequest? request = null;
            if (required >= today)
            {
                request = PurchaseRequest.CreateDraft(new UserId(schedule.OwnerId), required, schedule.Priority,
                    schedule.Items.Select(i => i.BuildItem()), occurrenceId, now);
                await requests.AddAsync(request, ct);
            }
            schedule.RecordOccurrence(occurrenceId, request?.Id.Value, now);
            processed++;
        }
        await lease.CommitAsync(ct);
        return processed;
    }
}
