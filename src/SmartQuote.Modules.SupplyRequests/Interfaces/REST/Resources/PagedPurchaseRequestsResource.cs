namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

public sealed record PagedPurchaseRequestsResource(
    IReadOnlyList<PurchaseRequestResource> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
