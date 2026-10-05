using SmartQuote.API.Shared.Domain.Repositories;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Application.Ports;

public interface IDeliveryEvaluationRepository : IBaseRepository<DeliveryEvaluation>
{
    Task<bool> ExistsForOrderAsync(PurchaseOrderId purchaseOrderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeliveryEvaluation>> ListBySupplierAsync(string taxIdentifier, CancellationToken cancellationToken = default);
}
