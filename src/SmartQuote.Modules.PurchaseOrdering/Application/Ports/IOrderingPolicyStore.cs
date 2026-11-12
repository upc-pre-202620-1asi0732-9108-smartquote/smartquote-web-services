using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;

namespace SmartQuote.API.PurchaseOrdering.Application.Ports;

public interface IOrderingPolicyLease : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
public interface IOrderingPolicyStore
{
    Task<IOrderingPolicyLease> LockAsync(Guid? requestId, string? supplierTaxIdentifier, CancellationToken ct);
    Task<SupplierAccount?> SupplierByTaxAsync(string taxIdentifier, CancellationToken ct);
    Task<SupplierAccount?> SupplierByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SupplierAccount>> ListSuppliersAsync(string? status, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<SupplierAccount>> SuppliersByTaxAsync(IReadOnlyList<string> identifiers, CancellationToken ct);
    void AddSupplier(SupplierAccount supplier);
    Task<PurchaseBudget?> BudgetAsync(Guid requestId, CancellationToken ct);
    void AddBudget(PurchaseBudget budget);
    Task<bool> HasOrderAsync(Guid requestId, CancellationToken ct);
}
public interface IOrderingRequestReader
{
    Task<bool> IsActiveAsync(Guid requestId, CancellationToken ct);
}
