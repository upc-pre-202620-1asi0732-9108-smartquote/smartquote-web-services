namespace SmartQuote.Modules.QuotationIntake.Application.Ports;

public interface IPurchaseRequestReferenceReader
{
    Task<PurchaseRequestReferenceData?> GetActiveAsync(Guid requestId, CancellationToken cancellationToken = default);
}

public sealed record PurchaseRequestReferenceData(
    Guid RequestId,
    long Version,
    string Status,
    IReadOnlySet<Guid> RequestedItemIds,
    IReadOnlyList<RequestedQuotationItem> Items);

public sealed record RequestedQuotationItem(Guid ItemId, string Description, decimal Quantity, string UnitOfMeasure,
    IReadOnlyList<RequestedQuotationRequirement> Requirements);

public sealed record RequestedQuotationRequirement(string Name, string ExpectedValue, string UnitOfMeasure, bool IsMandatory);
