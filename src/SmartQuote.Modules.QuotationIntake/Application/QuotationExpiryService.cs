using Microsoft.Extensions.Options;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.QuotationIntake.Application.Ports;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;

namespace SmartQuote.Modules.QuotationIntake.Application;

public sealed class QuotationExpiryOptions
{
    public bool Enabled { get; set; } = true;
    public int LeadDays { get; set; } = 3;
    public int PollSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 200;
    public string TimeZone { get; set; } = "America/Lima";
}
public sealed class QuotationExpiryService(IQuotationExpiryStore store, IAlertRequestReader requests,
    TimeProvider clock, IOptions<QuotationExpiryOptions> options, ICurrentUser user)
{
    public async Task ScanAsync(CancellationToken ct)
    {
        var o = options.Value;
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(o.TimeZone)).DateTime);
        var skip = 0;
        while (!ct.IsCancellationRequested)
        {
            var batch = await store.CandidatesAsync(today, today.AddDays(o.LeadDays), skip, o.BatchSize, ct);
            foreach (var quote in batch)
            {
                var request = await requests.ReadAsync(quote.RequestId, ct);
                if (request is { IsActive: true, IsAwarded: false, AssignedAnalystId: not null })
                    await store.UpsertAsync(quote, request.AssignedAnalystId.Value, o.LeadDays, today, now, ct);
            }
            if (batch.Count < o.BatchSize) break;
            skip += batch.Count;
        }
    }
    public Task<IReadOnlyList<QuotationExpiryAlert>> ListAsync(bool unreadOnly, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Page must be positive and pageSize between 1 and 100.");
        return store.ListAsync(user.UserId, unreadOnly, page, pageSize, ct);
    }
    public async Task ReadAsync(Guid id, CancellationToken ct)
    {
        var alert = await store.FindAsync(id, ct) ?? throw new NotFoundException("Quotation expiry alert was not found.");
        alert.Read(user.UserId, clock.GetUtcNow());
        await store.SaveAsync(ct);
    }
}
