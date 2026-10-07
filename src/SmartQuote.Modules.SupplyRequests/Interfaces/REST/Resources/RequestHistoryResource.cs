namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

public record RequestHistoryResource(Guid RequestId, IReadOnlyList<RequestStatusEntryResource> Entries);

public record RequestStatusEntryResource(
    string FromStatus,
    string ToStatus,
    Guid ChangedBy,
    DateTimeOffset ChangedAt,
    string Reason);
