using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Repositories;

public sealed class AdvancedEvaluationStore(EvaluationSimulationDbContext db) : IAdvancedEvaluationStore
{
    public async Task<IEvaluationLease> LockAsync(CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);
        try { await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731972101)", ct); return new Lease(db, tx); }
        catch { await tx.DisposeAsync(); throw; }
    }
    public void Add(SensitivityAnalysis analysis) => db.SensitivityAnalyses.Add(analysis);
    public Task<SensitivityAnalysis?> FindAsync(Guid id, CancellationToken ct) => db.SensitivityAnalyses.AsSplitQuery().SingleOrDefaultAsync(a => a.Id == id, ct);
    public async Task<IReadOnlyList<SensitivityAnalysis>> ListAsync(string requestId, CancellationToken ct) =>
        await db.SensitivityAnalyses.AsNoTracking().Where(a => a.RequestId == requestId).OrderByDescending(a => a.CreatedAt).Take(50).ToListAsync(ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    private sealed class Lease(EvaluationSimulationDbContext db, IDbContextTransaction tx) : IEvaluationLease
    {
        public async Task CommitAsync(CancellationToken ct) { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
}
