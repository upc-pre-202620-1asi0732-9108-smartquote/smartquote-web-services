using SmartQuote.API.Shared.Domain.Repositories;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;

namespace SmartQuote.API.SupplyRequests.Application.Ports;

public interface IPurchaseRequestRepository : IBaseRepository<PurchaseRequest>
{
    Task<PurchaseRequest?> GetByIdAsync(PurchaseRequestId requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseRequest>> ListAsync(Guid? requesterId, RequestStatus? status, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid? requesterId, RequestStatus? status, CancellationToken cancellationToken = default);
}
