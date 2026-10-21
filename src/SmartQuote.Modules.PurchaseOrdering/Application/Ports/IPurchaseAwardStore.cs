using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.PurchaseOrdering.Application.Ports;

public interface IPurchaseAwardStore
{
    Task<PurchaseAward?> ByIdAsync(Guid id, CancellationToken ct);
    Task<PurchaseAward?> ByRequestAsync(Guid requestId, CancellationToken ct);
    Task<PurchaseAward?> ByKeyAsync(string key, CancellationToken ct);
    Task<IReadOnlyList<PurchaseOrder>> OrdersAsync(Guid awardId, CancellationToken ct);
    void Add(PurchaseAward award);
    Task LockSuppliersAsync(IReadOnlyList<string> taxes, CancellationToken ct);
}
/// <summary>Infrastructure enlists the request and the durable events in the active ordering transaction.</summary>
public interface IAtomicAwardCommitter
{
    Task PrepareAsync(string requestId, string reference, IReadOnlyList<IDomainEvent> orderEvents,
        Func<CancellationToken, Task> validateLockedDecision, CancellationToken ct);
    Task AfterCommitAsync(CancellationToken ct);
}
