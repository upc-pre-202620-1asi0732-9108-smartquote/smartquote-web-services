using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.SupplyRequests.Application;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Transform;

namespace SmartQuote.API.SupplyRequests.Interfaces.REST;

public record CreateScheduleResource(DateOnly StartDate, int IntervalDays, int RequiredDateOffsetDays,
    string TimeZoneId, string Priority, IReadOnlyList<RequestedItemResource> Items);
public record ExpectedVersionResource(long ExpectedVersion);
public record EditDraftResource(DateOnly RequiredDate, string Priority, IReadOnlyList<RequestedItemResource> Items, long ExpectedVersion);
public record RecurringScheduleResource(Guid Id, Guid OwnerId, DateOnly StartDate, int IntervalDays, int RequiredDateOffsetDays,
    string TimeZoneId, string Priority, IReadOnlyList<RequestedItemResource> Items, bool IsActive, DateOnly NextOccurrenceDate,
    DateTimeOffset NextOccurrenceAt, DateTimeOffset CreatedAt, DateTimeOffset? PausedAt, long Version, IReadOnlyList<ScheduleOccurrence> Occurrences)
{
    public static RecurringScheduleResource From(RecurringRequestSchedule s) => new(s.Id, s.OwnerId, s.StartDate, s.IntervalDays,
        s.RequiredDateOffsetDays, s.TimeZoneId, s.Priority.ToString(), s.Items.Select(i => new RequestedItemResource(i.Description, i.Quantity,
            i.UnitOfMeasure, i.Requirements.Select(r => new TechnicalRequirementInputResource(r.Name, r.Operator.ToString(), r.ExpectedValue, r.UnitOfMeasure, r.IsMandatory)).ToList())).ToList(),
        s.IsActive, s.NextOccurrenceDate, s.NextOccurrenceAt, s.CreatedAt, s.PausedAt, s.Version, s.Occurrences);
}

[ApiController, Authorize(Roles = SmartQuoteRoles.ProductionSpecialist)]
[ProducesResponseType(typeof(ProblemDetails), 400)]
[ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403)]
[ProducesResponseType(typeof(ProblemDetails), 404)]
[ProducesResponseType(typeof(ProblemDetails), 409)]
[ProducesResponseType(typeof(ProblemDetails), 422)]
public sealed class RecurringRequestsController(RecurringRequestsService service, PurchaseRequestQueryService queries) : ControllerBase
{
    [HttpPost("api/v1/recurring-request-schedules")]
    [ProducesResponseType<RecurringScheduleResource>(201)]
    public async Task<ActionResult<RecurringScheduleResource>> Create(CreateScheduleResource r, CancellationToken ct)
    {
        var template = CreatePurchaseRequestCommandFromResourceAssembler.ToCommand(new(r.StartDate, r.Priority, r.Items));
        var schedule = await service.CreateAsync(r.StartDate, r.IntervalDays, r.RequiredDateOffsetDays, r.TimeZoneId, template, ct);
        return CreatedAtAction(nameof(Get), new { id = schedule.Id }, RecurringScheduleResource.From(schedule));
    }
    [HttpGet("api/v1/recurring-request-schedules")]
    [ProducesResponseType(typeof(IReadOnlyList<RecurringScheduleResource>), 200)]
    public async Task<ActionResult<IReadOnlyList<RecurringScheduleResource>>> List(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        Ok((await service.ListAsync(page, pageSize, ct)).Select(RecurringScheduleResource.From).ToList());
    [HttpGet("api/v1/recurring-request-schedules/{id:guid}")]
    [ProducesResponseType<RecurringScheduleResource>(200)]
    public async Task<ActionResult<RecurringScheduleResource>> Get(Guid id, CancellationToken ct) => Ok(RecurringScheduleResource.From(await service.GetAsync(id, ct)));
    [HttpPost("api/v1/recurring-request-schedules/{id:guid}/pause")]
    [ProducesResponseType<RecurringScheduleResource>(200)]
    public async Task<ActionResult<RecurringScheduleResource>> Pause(Guid id, ExpectedVersionResource resource, CancellationToken ct) =>
        Ok(RecurringScheduleResource.From(await service.PauseAsync(id, resource.ExpectedVersion, ct)));
    [HttpPut("api/v1/purchase-requests/{requestId:guid}/draft")]
    [ProducesResponseType<PurchaseRequestResource>(200)]
    public async Task<ActionResult<PurchaseRequestResource>> Edit(Guid requestId, EditDraftResource r, CancellationToken ct)
    {
        await service.EditDraftAsync(requestId, CreatePurchaseRequestCommandFromResourceAssembler.ToCommand(new(r.RequiredDate, r.Priority, r.Items)), r.ExpectedVersion, ct);
        return Ok(PurchaseRequestResourceFromViewAssembler.ToResource(await queries.GetByIdAsync(requestId, ct)));
    }
    [HttpPost("api/v1/purchase-requests/{requestId:guid}/submit")]
    [ProducesResponseType<PurchaseRequestResource>(200)]
    public async Task<ActionResult<PurchaseRequestResource>> Submit(Guid requestId, ExpectedVersionResource resource, CancellationToken ct)
    {
        await service.SubmitAsync(requestId, resource.ExpectedVersion, ct);
        return Ok(PurchaseRequestResourceFromViewAssembler.ToResource(await queries.GetByIdAsync(requestId, ct)));
    }
}
