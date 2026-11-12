using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Domain.Model.Entities;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Domain.Model.Events;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;

public class PurchaseRequest : AggregateRoot<PurchaseRequestId>
{
    private static readonly RequestStatus[] TerminalStatuses = [RequestStatus.Rejected, RequestStatus.Cancelled, RequestStatus.Ordered];

    private readonly List<RequestedItem> _items = [];
    private readonly List<RequestAttachment> _attachments = [];
    private readonly List<RequestStatusEntry> _statusHistory = [];
    private readonly List<RequestAssignmentEntry> _assignmentHistory = [];
    private readonly List<RequirementRevision> _requirementRevisions = [];

    private static readonly IReadOnlyDictionary<RequestStatus, RequestStatus[]> AllowedTransitions =
        new Dictionary<RequestStatus, RequestStatus[]>
        {
            [RequestStatus.Submitted] = [RequestStatus.UnderReview, RequestStatus.Cancelled],
            [RequestStatus.UnderReview] = [RequestStatus.QuotationCollection, RequestStatus.Rejected, RequestStatus.Cancelled],
            [RequestStatus.QuotationCollection] = [RequestStatus.Evaluation, RequestStatus.Rejected, RequestStatus.Cancelled],
            [RequestStatus.Evaluation] = [RequestStatus.QuotationCollection, RequestStatus.Approved, RequestStatus.Rejected, RequestStatus.Cancelled],
            [RequestStatus.Approved] = [RequestStatus.Evaluation, RequestStatus.Ordered, RequestStatus.Cancelled]
        };

    public UserId RequesterId { get; private set; } = null!;
    public DateOnly RequiredDate { get; private set; }
    public RequestPriority Priority { get; private set; }
    public RequestStatus Status { get; private set; }
    public long Version { get; private set; }
    public long TechnicalRevision { get; private set; }
    public Guid? AssignedAnalystId { get; private set; }
    public Guid? ScheduleOccurrenceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<RequestedItem> Items => _items.AsReadOnly();
    public IReadOnlyList<RequestAttachment> Attachments => _attachments.AsReadOnly();
    public IReadOnlyList<RequestStatusEntry> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyList<RequestAssignmentEntry> AssignmentHistory => _assignmentHistory.AsReadOnly();
    public IReadOnlyList<RequirementRevision> RequirementRevisions => _requirementRevisions.AsReadOnly();
    public bool IsActive => Status != RequestStatus.Draft && !TerminalStatuses.Contains(Status);

    private PurchaseRequest() { }

    public void AssignAnalyst(Guid analystId, Guid actorId, string? reason, DateTimeOffset now)
    {
        if (!IsActive) throw new DomainException("Only active submitted requests can be assigned.");
        if (analystId == Guid.Empty) throw new DomainException("An analyst is required.");
        if (AssignedAnalystId == analystId) throw new DomainException("This analyst is already responsible for the request.");
        if (AssignedAnalystId.HasValue && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Reassignment requires a reason.");
        if (reason?.Length > 2000) throw new DomainException("Assignment reason cannot exceed 2000 characters.");
        _assignmentHistory.Add(new(Guid.NewGuid(), AssignedAnalystId, analystId, actorId, now, reason?.Trim()));
        AssignedAnalystId = analystId;
        Touch(now);
    }

    public void ReviseRequirement(Guid clarificationId, Guid itemId, Guid requirementId,
        TechnicalRequirement replacement, Guid actorId, string reason, DateTimeOffset now)
    {
        if (!IsActive) throw new DomainException("Only active requests can have their requirements revised.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000) throw new DomainException("A revision reason of 1–2000 characters is required.");
        var item = _items.SingleOrDefault(i => i.Id.Value == itemId)
            ?? throw new DomainException("The item does not belong to this request.");
        var requirement = item.Requirements.SingleOrDefault(r => r.Id.Value == requirementId)
            ?? throw new DomainException("The requirement does not belong to this item.");
        if (item.Requirements.Any(r => r.Id != requirement.Id && r.Name.Equals(replacement.Name, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("A requirement with that name already exists.");
        if (!replacement.IsMandatory && !item.Requirements.Any(r => r.Id != requirement.Id && r.IsMandatory))
            throw new DomainException("Each item must retain at least one mandatory requirement.");
        var previous = RequirementDefinition.From(requirement);
        var current = RequirementDefinition.From(replacement);
        if (previous == current) throw new DomainException("The new specification must differ from the previous specification.");
        requirement.Revise(replacement);
        TechnicalRevision++;
        _requirementRevisions.Add(new(Guid.NewGuid(), clarificationId, itemId, requirementId, TechnicalRevision,
            previous, current, actorId, now, reason.Trim()));
        // An approval based on the previous interpretation must go back through evaluation.
        if (Status == RequestStatus.Approved)
            ChangeStatus(RequestStatus.Evaluation, new UserId(actorId), "Technical specification revised: " + reason.Trim());
        Touch(now);
    }

    public static PurchaseRequest CreateDraft(UserId requesterId, DateOnly requiredDate, RequestPriority priority,
        IEnumerable<RequestedItem> items, Guid occurrenceId, DateTimeOffset now)
    {
        var request = new PurchaseRequest { Id = new(Guid.NewGuid()), RequesterId = requesterId,
            RequiredDate = requiredDate, Priority = priority, Status = RequestStatus.Draft, Version = 1,
            ScheduleOccurrenceId = occurrenceId, CreatedAt = now, UpdatedAt = now };
        foreach (var item in items) request.AddItemCore(item);
        if (request._items.Count == 0) throw new DomainException("A request must contain at least one item.");
        return request;
    }

    public void UpdateDraft(DateOnly requiredDate, RequestPriority priority, IEnumerable<RequestedItem> items, DateTimeOffset now)
    {
        if (Status != RequestStatus.Draft) throw new DomainException("Only a draft can be edited.");
        var validated = CreateDraft(RequesterId, requiredDate, priority, items, ScheduleOccurrenceId ?? Guid.Empty, now);
        RequiredDate = requiredDate;
        Priority = priority;
        _items.Clear();
        _items.AddRange(validated.Items);
        Touch(now);
    }

    public void Submit(DateOnly localToday, DateTimeOffset now)
    {
        if (Status != RequestStatus.Draft) throw new DomainException("Only a draft can be submitted.");
        if (RequiredDate < localToday) throw new DomainException("Required date cannot be before the submission date.");
        if (_items.Count == 0 || _items.Any(i => !i.HasMandatoryRequirement()))
            throw new DomainException("Each request item needs a mandatory requirement.");
        Status = RequestStatus.Submitted;
        _statusHistory.Add(RequestStatusEntry.Create(RequestStatus.Draft, Status, RequesterId, "Specialist confirmed recurring request."));
        Touch(now);
        AddDomainEvent(new PurchaseRequestSubmitted(Id, RequesterId, now));
    }

    private void Touch(DateTimeOffset now) { Version++; UpdatedAt = now; }

    public static PurchaseRequest Create(
        UserId requesterId,
        DateOnly requiredDate,
        RequestPriority priority,
        IEnumerable<RequestedItem> items)
    {
        if (requiredDate < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new DomainException("Required date cannot be before the registration date.");

        var itemList = items.ToList();
        if (itemList.Count == 0)
            throw new DomainException("A purchase request must contain at least one item.");

        var request = new PurchaseRequest
        {
            Id = new PurchaseRequestId(Guid.NewGuid()),
            RequesterId = requesterId,
            RequiredDate = requiredDate,
            Priority = priority,
            Status = RequestStatus.Submitted,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
        request.UpdatedAt = request.CreatedAt;

        foreach (var item in itemList)
            request.AddItemCore(item);

        request._statusHistory.Add(RequestStatusEntry.Create(
            RequestStatus.Draft,
            RequestStatus.Submitted,
            requesterId,
            "Purchase request submitted."));

        request.AddDomainEvent(new PurchaseRequestSubmitted(request.Id, requesterId, request.CreatedAt));

        return request;
    }

    public void AddItem(RequestedItem item)
    {
        AddItemCore(item);
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddAttachment(RequestAttachment attachment)
    {
        _attachments.Add(attachment);
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeStatus(RequestStatus nextStatus, UserId changedBy, string reason)
    {
        if (TerminalStatuses.Contains(Status))
            throw new DomainException($"Purchase request in status '{Status}' cannot change status any further.");

        if (nextStatus == Status)
            throw new DomainException("The next status must be different from the current status.");

        if (!AllowedTransitions.TryGetValue(Status, out var transitions) || !transitions.Contains(nextStatus))
            throw new DomainException($"Transition from '{Status}' to '{nextStatus}' is not allowed.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A reason is required when the request status changes.");

        var previousStatus = Status;

        _statusHistory.Add(RequestStatusEntry.Create(previousStatus, nextStatus, changedBy, reason));
        Status = nextStatus;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PurchaseRequestStatusChanged(Id, RequesterId, previousStatus, nextStatus, reason, changedBy, UpdatedAt));
    }

    private void AddItemCore(RequestedItem item)
    {
        if (!item.HasMandatoryRequirement())
            throw new DomainException("Each requested item must declare at least one mandatory technical requirement.");

        item.AssignLineNumber(_items.Count + 1);
        _items.Add(item);
    }
}
