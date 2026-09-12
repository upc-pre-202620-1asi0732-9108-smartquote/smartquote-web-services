namespace SmartQuote.Modules.QuotationIntake.Application.Ports;

public interface IPurchaseRequestReferenceReader
{
    Task<PurchaseRequestReferenceData?> GetActiveAsync(Guid requestId, CancellationToken cancellationToken = default);
}

public sealed record PurchaseRequestReferenceData(
    Guid RequestId,
    long Version,
    string Status,
    IReadOnlySet<Guid> RequestedItemIds);
