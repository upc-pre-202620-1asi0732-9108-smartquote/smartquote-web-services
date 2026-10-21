using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.Entities;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Application;

public sealed class RequestCollaborationService(IPurchaseRequestRepository requests, ISupplyWorkflowStore store,
    ISupplyRequestsUnitOfWork uow, IRequestUserDirectory users, ICurrentUser actor, TimeProvider clock, IDomainEventDispatcher events)
{
    public static void CheckVersion(long actual, long expected)
    {
        if (expected <= 0) throw new ArgumentException("Expected version must be positive.");
        if (actual != expected) throw new ConflictException($"Expected version {expected}, current version is {actual}.");
    }
    private async Task RequireActiveRole(Guid id, string role, CancellationToken ct)
    {
        if (!await users.IsActiveInRoleAsync(id, role, ct))
            throw new DomainException("The account must be active and have the required role.");
    }
    private async Task<PurchaseRequest> Request(Guid id, CancellationToken ct) =>
        await requests.GetByIdAsync(new(id), ct) ?? throw new NotFoundException("Purchase request was not found.");
    private async Task<TechnicalClarification> Clarification(Guid id, CancellationToken ct) =>
        await store.GetClarificationAsync(id, ct) ?? throw new NotFoundException("Technical clarification was not found.");

    public async Task AssignAsync(Guid requestId, Guid analystId, string? reason, long version, CancellationToken ct)
    {
        if (!actor.IsInRole(SmartQuoteRoles.PurchaseManager)) throw new UnauthorizedAccessException();
        await RequireActiveRole(actor.UserId, SmartQuoteRoles.PurchaseManager, ct);
        await RequireActiveRole(analystId, SmartQuoteRoles.PurchaseAnalyst, ct);
        var request = await Request(requestId, ct);
        CheckVersion(request.Version, version);
        request.AssignAnalyst(analystId, actor.UserId, reason, clock.GetUtcNow());
        await uow.CompleteAsync(ct);
    }

    public async Task<IReadOnlyList<RequestAssignmentEntry>> AssignmentHistoryAsync(Guid requestId, CancellationToken ct)
    {
        if (!IsPurchasing()) throw new UnauthorizedAccessException();
        return (await Request(requestId, ct)).AssignmentHistory;
    }

    public async Task<TechnicalClarification> RegisterAsync(Guid requestId, Guid itemId, Guid requirementId,
        Guid specialistId, string question, long version, CancellationToken ct)
    {
        if (!actor.IsInRole(SmartQuoteRoles.PurchaseAnalyst)) throw new UnauthorizedAccessException();
        await RequireActiveRole(actor.UserId, SmartQuoteRoles.PurchaseAnalyst, ct);
        var request = await Request(requestId, ct);
        CheckVersion(request.Version, version);
        if (!request.IsActive) throw new DomainException("The request is not active.");
        if (request.RequesterId.Value != specialistId)
            throw new UnauthorizedAccessException("The recipient must be the requesting specialist.");
        await RequireActiveRole(specialistId, SmartQuoteRoles.ProductionSpecialist, ct);
        var item = request.Items.SingleOrDefault(i => i.Id.Value == itemId);
        if (item is null || !item.Requirements.Any(r => r.Id.Value == requirementId))
            throw new DomainException("The item and requirement must belong to this request.");
        var clarification = TechnicalClarification.Create(requestId, itemId, requirementId, actor.UserId,
            specialistId, question, request.TechnicalRevision, clock.GetUtcNow());
        store.AddClarification(clarification);
        // The persisted clarification is also the recipient's pending notice; no event can lose it.
        await uow.CompleteAsync(ct);
        return clarification;
    }

    public async Task<IReadOnlyList<TechnicalClarification>> ListAsync(Guid? requestId, string? status, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Invalid pagination.");
        if (status is not null && status is not ("Pending" or "Answered" or "RevisionApplied")) throw new ArgumentException("Invalid clarification status.");
        if (requestId.HasValue)
        {
            var request = await Request(requestId.Value, ct);
            if (!IsPurchasing() && request.RequesterId.Value != actor.UserId) throw new UnauthorizedAccessException();
        }
        if (!IsPurchasing() && !actor.IsInRole(SmartQuoteRoles.ProductionSpecialist)) throw new UnauthorizedAccessException();
        return await store.ListClarificationsAsync(requestId, IsPurchasing() ? null : actor.UserId, status, page, pageSize, ct);
    }

    public async Task<TechnicalClarification> RespondAsync(Guid id, string answer, long version, CancellationToken ct)
    {
        var clarification = await RecipientAsync(id, ct);
        CheckVersion(clarification.Version, version);
        if (!(await Request(clarification.RequestId, ct)).IsActive) throw new DomainException("The request is not active.");
        clarification.Respond(answer, actor.UserId, clock.GetUtcNow());
        await uow.CompleteAsync(ct);
        return clarification;
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var clarification = await RecipientAsync(id, ct);
        clarification.MarkRead(clock.GetUtcNow());
        await uow.CompleteAsync(ct);
    }

    public async Task<RequirementRevision> ReviseAsync(Guid id, TechnicalRequirement replacement, string reason,
        long clarificationVersion, long requestVersion, CancellationToken ct)
    {
        var clarification = await RecipientAsync(id, ct);
        var request = await Request(clarification.RequestId, ct);
        CheckVersion(clarification.Version, clarificationVersion);
        CheckVersion(request.Version, requestVersion);
        if (clarification.SourceTechnicalRevision != request.TechnicalRevision)
            throw new ConflictException("The technical specification changed after this consultation. Register a new clarification.");
        clarification.MarkRevisionApplied();
        request.ReviseRequirement(id, clarification.ItemId, clarification.RequirementId, replacement, actor.UserId, reason, clock.GetUtcNow());
        await uow.CompleteAsync(ct);
        await events.DispatchAsync(request.DomainEvents, ct);
        request.ClearDomainEvents();
        return request.RequirementRevisions.Last();
    }

    public async Task<IReadOnlyList<RequirementRevision>> RevisionsAsync(Guid requestId, CancellationToken ct)
    {
        var request = await Request(requestId, ct);
        if (!IsPurchasing() && request.RequesterId.Value != actor.UserId) throw new UnauthorizedAccessException();
        return request.RequirementRevisions;
    }
    private bool IsPurchasing() => actor.IsInRole(SmartQuoteRoles.PurchaseAnalyst) || actor.IsInRole(SmartQuoteRoles.PurchaseManager);
    private async Task<TechnicalClarification> RecipientAsync(Guid id, CancellationToken ct)
    {
        if (!actor.IsInRole(SmartQuoteRoles.ProductionSpecialist)) throw new UnauthorizedAccessException();
        var clarification = await Clarification(id, ct);
        if (clarification.SpecialistId != actor.UserId) throw new UnauthorizedAccessException("Only the recipient can act on this clarification.");
        await RequireActiveRole(actor.UserId, SmartQuoteRoles.ProductionSpecialist, ct);
        return clarification;
    }
}
