using SmartQuote.API.Shared.Domain.Repositories;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Application.Ports;

public interface IPurchaseOrderRepository : IBaseRepository<PurchaseOrder>
{
    Task<PurchaseOrder?> GetByIdAsync(PurchaseOrderId purchaseOrderId, CancellationToken cancellationToken = default);

    Task<PurchaseOrder?> FindBySimulationAsync(string simulationRunId, CancellationToken cancellationToken = default);

    Task<PurchaseOrder?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}
