using Microsoft.EntityFrameworkCore;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Repositories;

public sealed class PurchaseAwardStore(PurchaseOrderingDbContext db) : IPurchaseAwardStore
{
    public Task<PurchaseAward?> ByIdAsync(Guid id, CancellationToken ct) => db.PurchaseAwards.SingleOrDefaultAsync(a => a.Id == id, ct);
    public Task<PurchaseAward?> ByRequestAsync(Guid requestId, CancellationToken ct) => db.PurchaseAwards.SingleOrDefaultAsync(a => a.RequestId == requestId, ct);
    public Task<PurchaseAward?> ByKeyAsync(string key, CancellationToken ct) => db.PurchaseAwards.SingleOrDefaultAsync(a => a.IdempotencyKey == key, ct);
    public async Task<IReadOnlyList<PurchaseOrder>> OrdersAsync(Guid awardId, CancellationToken ct) => await db.PurchaseOrders.Include(o => o.Lines)
        .Where(o => o.AwardId == awardId).OrderBy(o => o.OrderNumber).ToListAsync(ct);
    public void Add(PurchaseAward award) => db.PurchaseAwards.Add(award);
    public async Task LockSuppliersAsync(IReadOnlyList<string> taxes, CancellationToken ct)
    {
        foreach (var tax in taxes.Distinct().Order(StringComparer.Ordinal))
        {
            var key = $"smartquote:supplier:{tax}";
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
        }
    }
}
