namespace SmartQuote.API.SupplyRequests.Application.OutboundServices;

/// <summary>
/// Published contract: the only way other bounded contexts may read SupplyRequests state.
/// </summary>
public interface IPurchaseRequestSnapshotProvider
{
    Task<PurchaseRequestSnapshot?> GetSnapshotAsync(Guid requestId, CancellationToken cancellationToken = default);
}
