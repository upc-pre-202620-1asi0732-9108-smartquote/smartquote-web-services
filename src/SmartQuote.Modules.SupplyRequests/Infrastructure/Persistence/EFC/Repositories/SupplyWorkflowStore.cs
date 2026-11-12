using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Repositories;

public sealed class SupplyWorkflowStore(SupplyRequestsDbContext db) : ISupplyWorkflowStore
{
    public Task<TechnicalClarification?> GetClarificationAsync(Guid id, CancellationToken ct) => db.TechnicalClarifications.SingleOrDefaultAsync(c => c.Id == id, ct);
    public async Task<IReadOnlyList<TechnicalClarification>> ListClarificationsAsync(Guid? requestId, Guid? recipientId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.TechnicalClarifications.AsNoTracking();
        if (requestId.HasValue)
        {
            var reference = new SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects.PurchaseRequestId(requestId.Value);
            query = query.Where(c => c.RequestId == reference);
        }
        if (recipientId.HasValue) query = query.Where(c => c.SpecialistId == recipientId);
        if (status is not null) query = query.Where(c => c.Status == status);
        return await query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }
    public void AddClarification(TechnicalClarification clarification) => db.TechnicalClarifications.Add(clarification);
    public void AddSchedule(RecurringRequestSchedule schedule) => db.RecurringRequestSchedules.Add(schedule);
    public async Task<IReadOnlyList<RecurringRequestSchedule>> ListSchedulesAsync(Guid owner, int page, int pageSize, CancellationToken ct) =>
        await db.RecurringRequestSchedules.AsNoTracking().Where(s => s.OwnerId == owner).OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    public Task<RecurringRequestSchedule?> GetScheduleAsync(Guid id, CancellationToken ct) => db.RecurringRequestSchedules.Include(s => s.Occurrences).SingleOrDefaultAsync(s => s.Id == id, ct);
    public Task<RecurringRequestSchedule?> GetScheduleForOccurrenceAsync(Guid occurrenceId, CancellationToken ct) => db.RecurringRequestSchedules.AsNoTracking()
        .SingleOrDefaultAsync(s => s.Occurrences.Any(o => o.Id == occurrenceId), ct);
    public async Task<IReadOnlyList<Guid>> GetDueScheduleIdsAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await db.RecurringRequestSchedules.AsNoTracking().Where(s => s.IsActive && s.NextOccurrenceAt <= now)
            .OrderBy(s => s.NextOccurrenceAt).ThenBy(s => s.Id).Take(limit).Select(s => s.Id).ToListAsync(ct);

    public async Task<IScheduleLease> LockScheduleAsync(Guid id, CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var schedule = await db.RecurringRequestSchedules.FromSqlInterpolated($"SELECT * FROM supply_requests.recurring_request_schedules WHERE id = {id} FOR UPDATE")
                .AsSplitQuery().Include(s => s.Occurrences).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Schedule was not found.");
            return new Lease(db, tx, schedule);
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    private sealed class Lease(SupplyRequestsDbContext db, IDbContextTransaction tx, RecurringRequestSchedule schedule) : IScheduleLease
    {
        public RecurringRequestSchedule Schedule => schedule;
        public async Task CommitAsync(CancellationToken ct) { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
}
