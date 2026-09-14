using SmartQuote.API.SupplyRequests.Application.OutboundServices;

namespace SmartQuote.API.EvaluationSimulation.Application.Ports;

public interface IPurchaseRequestSnapshotReader
{
    Task<PurchaseRequestSnapshot?> GetCurrentAsync(string requestId, CancellationToken cancellationToken = default);
}
