using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.SupplyRequests.Application;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.Entities;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Transform;

namespace SmartQuote.API.SupplyRequests.Interfaces.REST;

public record AssignAnalystResource(Guid AnalystId, string? Reason, long ExpectedVersion);
public record CreateClarificationResource(Guid ItemId, Guid RequirementId, Guid SpecialistId, string Question, long ExpectedVersion);
public record ClarificationResponseResource(string Answer, long ExpectedVersion);
public record RequirementRevisionResource(TechnicalRequirementInputResource Specification, string Reason,
    long ExpectedClarificationVersion, long ExpectedRequestVersion);
public record ClarificationResource(Guid Id, Guid RequestId, Guid ItemId, Guid RequirementId, Guid AuthorId,
    Guid SpecialistId, string Question, string Status, DateTimeOffset CreatedAt, DateTimeOffset? NoticeReadAt,
    string? Answer, Guid? AnsweredBy, DateTimeOffset? AnsweredAt, long SourceTechnicalRevision, long Version)
{
    public static ClarificationResource From(TechnicalClarification c) => new(c.Id, c.RequestId.Value, c.ItemId,
        c.RequirementId, c.AuthorId, c.SpecialistId, c.Question, c.Status, c.CreatedAt, c.NoticeReadAt,
        c.Answer, c.AnsweredBy, c.AnsweredAt, c.SourceTechnicalRevision, c.Version);
}

[ApiController, Authorize]
[ProducesResponseType(typeof(ProblemDetails), 400)]
[ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403)]
[ProducesResponseType(typeof(ProblemDetails), 404)]
[ProducesResponseType(typeof(ProblemDetails), 409)]
[ProducesResponseType(typeof(ProblemDetails), 422)]
public sealed class RequestCollaborationController(RequestCollaborationService service, PurchaseRequestQueryService queries) : ControllerBase
{
    [HttpPut("api/v1/purchase-requests/{requestId:guid}/assignment"), Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
    [ProducesResponseType<PurchaseRequestResource>(200)]
    public async Task<ActionResult<PurchaseRequestResource>> Assign(Guid requestId, AssignAnalystResource resource, CancellationToken ct)
    {
        await service.AssignAsync(requestId, resource.AnalystId, resource.Reason, resource.ExpectedVersion, ct);
        return Ok(PurchaseRequestResourceFromViewAssembler.ToResource(await queries.GetByIdAsync(requestId, ct)));
    }
    [HttpGet("api/v1/purchase-requests/{requestId:guid}/assignment-history"), Authorize(Roles = SmartQuoteRoles.PurchasingStaff)]
    [ProducesResponseType(typeof(IReadOnlyList<RequestAssignmentEntry>), 200)]
    public async Task<ActionResult<IReadOnlyList<RequestAssignmentEntry>>> Assignments(Guid requestId, CancellationToken ct) =>
        Ok(await service.AssignmentHistoryAsync(requestId, ct));

    [HttpPost("api/v1/purchase-requests/{requestId:guid}/clarifications"), Authorize(Roles = SmartQuoteRoles.PurchaseAnalyst)]
    [ProducesResponseType<ClarificationResource>(201)]
    public async Task<ActionResult<ClarificationResource>> Create(Guid requestId, CreateClarificationResource resource, CancellationToken ct)
    {
        var result = await service.RegisterAsync(requestId, resource.ItemId, resource.RequirementId, resource.SpecialistId,
            resource.Question, resource.ExpectedVersion, ct);
        return Created($"/api/v1/purchase-requests/{requestId}/clarifications", ClarificationResource.From(result));
    }
    [HttpGet("api/v1/purchase-requests/{requestId:guid}/clarifications")]
    [ProducesResponseType(typeof(IReadOnlyList<ClarificationResource>), 200)]
    public async Task<ActionResult<IReadOnlyList<ClarificationResource>>> ForRequest(Guid requestId, string? status = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default) => Ok((await service.ListAsync(requestId, status, page, pageSize, ct)).Select(ClarificationResource.From).ToList());

    [HttpGet("api/v1/technical-clarifications"), Authorize(Roles = SmartQuoteRoles.ProductionSpecialist)]
    [ProducesResponseType(typeof(IReadOnlyList<ClarificationResource>), 200)]
    public async Task<ActionResult<IReadOnlyList<ClarificationResource>>> Inbox(bool assignedToMe = true, string? status = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (!assignedToMe) throw new ArgumentException("Only the recipient's inbox is available.");
        return Ok((await service.ListAsync(null, status, page, pageSize, ct)).Select(ClarificationResource.From).ToList());
    }
    [HttpPut("api/v1/technical-clarifications/{id:guid}/read"), Authorize(Roles = SmartQuoteRoles.ProductionSpecialist)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct) { await service.MarkReadAsync(id, ct); return NoContent(); }

    [HttpPost("api/v1/technical-clarifications/{id:guid}/response"), Authorize(Roles = SmartQuoteRoles.ProductionSpecialist)]
    [ProducesResponseType<ClarificationResource>(200)]
    public async Task<ActionResult<ClarificationResource>> Respond(Guid id, ClarificationResponseResource resource, CancellationToken ct) =>
        Ok(ClarificationResource.From(await service.RespondAsync(id, resource.Answer, resource.ExpectedVersion, ct)));

    [HttpPost("api/v1/technical-clarifications/{id:guid}/requirement-revisions"), Authorize(Roles = SmartQuoteRoles.ProductionSpecialist)]
    [ProducesResponseType<RequirementRevision>(201)]
    public async Task<ActionResult<RequirementRevision>> Revise(Guid id, RequirementRevisionResource resource, CancellationToken ct)
    {
        var r = resource.Specification;
        var replacement = TechnicalRequirement.Create(r.Name, EnumResourceParser.Parse<ComparisonOperator>(r.Operator, "operator"), r.ExpectedValue, r.UnitOfMeasure, r.IsMandatory);
        var result = await service.ReviseAsync(id, replacement, resource.Reason, resource.ExpectedClarificationVersion, resource.ExpectedRequestVersion, ct);
        return StatusCode(201, result);
    }
    [HttpGet("api/v1/purchase-requests/{requestId:guid}/requirement-revisions")]
    [ProducesResponseType(typeof(IReadOnlyList<RequirementRevision>), 200)]
    public async Task<ActionResult<IReadOnlyList<RequirementRevision>>> Revisions(Guid requestId, CancellationToken ct) => Ok(await service.RevisionsAsync(requestId, ct));
}
