using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Repositories;

public class DeliveryEvaluationRepository(PurchaseOrderingDbContext context)
    : BaseRepository<DeliveryEvaluation, PurchaseOrderingDbContext>(context), IDeliveryEvaluationRepository
{
    public async Task<bool> ExistsForOrderAsync(PurchaseOrderId purchaseOrderId, CancellationToken cancellationToken = default) =>
        await Context.DeliveryEvaluations.AnyAsync(evaluation => evaluation.PurchaseOrderId == purchaseOrderId, cancellationToken);

    public async Task<IReadOnlyList<DeliveryEvaluation>> ListBySupplierAsync(string taxIdentifier, CancellationToken cancellationToken = default) =>
        await Context.DeliveryEvaluations
            .Where(evaluation => evaluation.SupplierTaxIdentifier == taxIdentifier)
            .OrderBy(evaluation => evaluation.EvaluatedAt)
            .ToListAsync(cancellationToken);
}
