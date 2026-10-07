using SmartQuote.API.SupplyRequests.Application.OutboundServices;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.ReferenceReaders;

public class SupplyRequestSnapshotAdapter(IPurchaseRequestSnapshotProvider snapshotProvider) : IPurchaseRequestSnapshotReader
{
    public Task<PurchaseRequestSnapshot?> GetCurrentAsync(string requestId, CancellationToken cancellationToken = default) =>
        snapshotProvider.GetSnapshotAsync(Guid.Parse(requestId), cancellationToken);
}
