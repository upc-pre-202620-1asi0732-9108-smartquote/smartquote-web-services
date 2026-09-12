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
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<RequestedItem> Items => _items.AsReadOnly();
    public IReadOnlyList<RequestAttachment> Attachments => _attachments.AsReadOnly();
    public IReadOnlyList<RequestStatusEntry> StatusHistory => _statusHistory.AsReadOnly();

    private PurchaseRequest() { }

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

        request.AddDomainEvent(new PurchaseRequestSubmitted(request.Id, request.CreatedAt));

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

        AddDomainEvent(new PurchaseRequestStatusChanged(Id, RequesterId, previousStatus, nextStatus, reason, UpdatedAt));
    }

    private void AddItemCore(RequestedItem item)
    {
        if (!item.HasMandatoryRequirement())
            throw new DomainException("Each requested item must declare at least one mandatory technical requirement.");

        item.AssignLineNumber(_items.Count + 1);
        _items.Add(item);
    }
}
