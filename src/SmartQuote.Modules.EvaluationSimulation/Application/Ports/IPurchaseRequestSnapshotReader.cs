using SmartQuote.API.SupplyRequests.Application.OutboundServices;

namespace SmartQuote.Modules.EvaluationSimulation.Application.Ports;

public interface IPurchaseRequestSnapshotReader
{
    Task<PurchaseRequestSnapshot?> GetCurrentAsync(string requestId, CancellationToken cancellationToken = default);
}
