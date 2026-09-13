using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Repositories;

public class PurchaseOrderRepository(PurchaseOrderingDbContext context)
    : BaseRepository<PurchaseOrder, PurchaseOrderingDbContext>(context), IPurchaseOrderRepository
{
    public async Task<PurchaseOrder?> GetByIdAsync(PurchaseOrderId purchaseOrderId, CancellationToken cancellationToken = default) =>
        await Context.PurchaseOrders
            .Include(order => order.Lines)
            .FirstOrDefaultAsync(order => order.Id == purchaseOrderId, cancellationToken);

    public async Task<PurchaseOrder?> FindBySimulationAsync(string simulationRunId, CancellationToken cancellationToken = default) =>
        await Context.PurchaseOrders
            .Include(order => order.Lines)
            .Where(order => order.SourceDecision.SimulationRunId == simulationRunId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PurchaseOrder?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        await Context.PurchaseOrders
            .Include(order => order.Lines)
            .Where(order => order.Approval.IdempotencyKey == idempotencyKey)
            .FirstOrDefaultAsync(cancellationToken);
}
