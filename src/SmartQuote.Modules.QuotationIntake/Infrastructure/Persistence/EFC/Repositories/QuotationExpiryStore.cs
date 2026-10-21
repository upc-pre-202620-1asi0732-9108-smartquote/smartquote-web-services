using Microsoft.EntityFrameworkCore;
using SmartQuote.Modules.QuotationIntake.Application.Ports;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Enums;
using SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Repositories;

public sealed class QuotationExpiryStore(QuotationIntakeDbContext db) : IQuotationExpiryStore
{
    public async Task<IReadOnlyList<ExpiringQuotation>> CandidatesAsync(DateOnly today, DateOnly through, int skip, int batchSize, CancellationToken ct) =>
        await db.PoultryQuotes.AsNoTracking().Where(q => q.Status == QuotationStatus.Verified && q.ValidUntil >= today && q.ValidUntil <= through)
            .OrderBy(q => q.ValidUntil).ThenBy(q => q.Id).Skip(skip).Take(batchSize).Select(q => new ExpiringQuotation(q.Id.Value,
                Guid.Parse(q.RequestReference.RequestId), q.Supplier.BusinessName, q.ValidUntil!.Value)).ToListAsync(ct);
    public async Task UpsertAsync(ExpiringQuotation quote, Guid recipient, int leadDays, DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        var alert = QuotationExpiryAlert.Create(quote.QuotationId, quote.RequestId, recipient, quote.SupplierName, quote.ValidUntil, leadDays, today, now);
        // Database uniqueness handles concurrent workers. Reassignment preserves id, creation time and reading state.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO quotation_intake.quotation_expiry_alerts (id, quotation_id, request_id, recipient_id, supplier_name, valid_until, lead_days, created_at)
            VALUES ({alert.Id}, {quote.QuotationId}, {quote.RequestId}, {recipient}, {quote.SupplierName}, {quote.ValidUntil}, {leadDays}, {now})
            ON CONFLICT (quotation_id, valid_until, lead_days) DO UPDATE
            SET recipient_id = EXCLUDED.recipient_id, reassigned_at = EXCLUDED.created_at
            WHERE quotation_expiry_alerts.recipient_id <> EXCLUDED.recipient_id
            """, ct);
    }
    public async Task<IReadOnlyList<QuotationExpiryAlert>> ListAsync(Guid recipient, bool unreadOnly, int page, int pageSize, CancellationToken ct) =>
        await db.QuotationExpiryAlerts.AsNoTracking().Where(a => a.RecipientId == recipient && (!unreadOnly || a.ReadAt == null))
            .OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    public Task<QuotationExpiryAlert?> FindAsync(Guid id, CancellationToken ct) => db.QuotationExpiryAlerts.SingleOrDefaultAsync(a => a.Id == id, ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
