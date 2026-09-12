using SmartQuote.API.SupplyRequests.Application.OutboundServices;
using SmartQuote.Modules.QuotationIntake.Application.Ports;

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.ReferenceReaders;

/// <summary>
/// In-process adapter: calls SupplyRequests' published contract directly (same deployable,
/// no HTTP hop), matching the "SupplyRequestsPublicContract" dependency.
/// </summary>
public class SupplyRequestReferenceAdapter(IPurchaseRequestSnapshotProvider snapshotProvider) : IPurchaseRequestReferenceReader
{
    private static readonly string[] IntakeStatuses = ["QuotationCollection", "Evaluation"];

    public async Task<PurchaseRequestReferenceData?> GetActiveAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotProvider.GetSnapshotAsync(requestId, cancellationToken);
        if (snapshot is null || !IntakeStatuses.Contains(snapshot.Status))
            return null;

        return new PurchaseRequestReferenceData(
            requestId,
            snapshot.Version,
            snapshot.Status,
            snapshot.Items.Select(item => Guid.Parse(item.ItemId)).ToHashSet());
    }
}

