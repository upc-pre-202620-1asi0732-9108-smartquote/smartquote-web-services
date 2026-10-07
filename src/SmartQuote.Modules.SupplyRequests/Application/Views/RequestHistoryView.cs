namespace SmartQuote.API.SupplyRequests.Application.Views;

public record RequestHistoryView(Guid RequestId, IReadOnlyList<RequestStatusEntryView> Entries);

public record RequestStatusEntryView(
    string FromStatus,
    string ToStatus,
    Guid ChangedBy,
    DateTimeOffset ChangedAt,
    string Reason);
