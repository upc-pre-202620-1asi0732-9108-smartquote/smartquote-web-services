using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Repositories;

public sealed class OrderingPolicyStore(PurchaseOrderingDbContext db) : IOrderingPolicyStore
{
    public async Task<IOrderingPolicyLease> LockAsync(Guid? requestId, string? supplierTaxIdentifier, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        IDbContextTransaction? transaction = null;
        var requestKey = requestId.HasValue ? $"smartquote:acquisition:{requestId.Value:D}" : null;
        var requestLocked = false;
        try
        {
            // Serialize even a missing budget/supplier row, so concurrent first-time creation is safe.
            // Approval always takes request first, supplier second. Individual decisions take one key.
            if (requestKey is not null)
            {
                // Keep request serialization through post-commit lifecycle recovery as well.
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock(hashtextextended({requestKey}, 0))", ct);
                requestLocked = true;
            }
            transaction = await db.Database.BeginTransactionAsync(ct);
            if (supplierTaxIdentifier is not null)
            {
                var key = $"smartquote:supplier:{supplierTaxIdentifier}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            }
            return new Lease(db, transaction, requestKey);
        }
        catch
        {
            if (transaction is not null) await transaction.DisposeAsync();
            try
            {
                if (requestLocked) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock(hashtextextended({requestKey}, 0))", CancellationToken.None);
            }
            finally { await db.Database.CloseConnectionAsync(); }
            throw;
        }
    }
    public Task<SupplierAccount?> SupplierByTaxAsync(string taxIdentifier, CancellationToken ct) => db.SupplierAccounts.Include(s => s.History).SingleOrDefaultAsync(s => s.TaxIdentifier == taxIdentifier, ct);
    public Task<SupplierAccount?> SupplierByIdAsync(Guid id, CancellationToken ct) => db.SupplierAccounts.AsNoTracking().Include(s => s.History).SingleOrDefaultAsync(s => s.Id == id, ct);
    public async Task<IReadOnlyList<SupplierAccount>> ListSuppliersAsync(string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.SupplierAccounts.AsNoTracking();
        if (status is not null) query = query.Where(s => s.Status == status);
        return await query.OrderBy(s => s.TaxIdentifier).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<SupplierAccount>> SuppliersByTaxAsync(IReadOnlyList<string> identifiers, CancellationToken ct) =>
        await db.SupplierAccounts.AsNoTracking().Where(s => identifiers.Contains(s.TaxIdentifier)).ToListAsync(ct);
    public void AddSupplier(SupplierAccount supplier) => db.SupplierAccounts.Add(supplier);
    public Task<PurchaseBudget?> BudgetAsync(Guid requestId, CancellationToken ct) => db.PurchaseBudgets.Include(b => b.Revisions).SingleOrDefaultAsync(b => b.RequestId == requestId, ct);
    public void AddBudget(PurchaseBudget budget) => db.PurchaseBudgets.Add(budget);
    public Task<bool> HasOrderAsync(Guid requestId, CancellationToken ct) => db.PurchaseOrders.AnyAsync(o => o.SourceDecision.PurchaseRequestId == requestId.ToString(), ct);
    private sealed class Lease(PurchaseOrderingDbContext db, IDbContextTransaction tx, string? requestKey) : IOrderingPolicyLease
    {
        public async Task CommitAsync(CancellationToken ct) { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        public async ValueTask DisposeAsync()
        {
            try
            {
                await tx.DisposeAsync();
                if (requestKey is not null)
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock(hashtextextended({requestKey}, 0))", CancellationToken.None);
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
