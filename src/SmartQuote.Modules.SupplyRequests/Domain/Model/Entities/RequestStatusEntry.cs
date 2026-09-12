using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Entities;

public class RequestStatusEntry
{
    public RequestStatus FromStatus { get; private set; }
    public RequestStatus ToStatus { get; private set; }
    public UserId ChangedBy { get; private set; } = null!;
    public DateTimeOffset ChangedAt { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    private RequestStatusEntry() { }

    private RequestStatusEntry(RequestStatus fromStatus, RequestStatus toStatus, UserId changedBy, string reason)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedBy = changedBy;
        ChangedAt = DateTimeOffset.UtcNow;
        Reason = reason;
    }

    public static RequestStatusEntry Create(RequestStatus fromStatus, RequestStatus toStatus, UserId changedBy, string reason) =>
        new(fromStatus, toStatus, changedBy, reason);
}
