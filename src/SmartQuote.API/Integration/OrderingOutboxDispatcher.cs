using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Events;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Auditing;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Domain.Model.Events;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.API.Integration;

public sealed class OrderingOutboxDispatcher(IServiceScopeFactory scopes, ILogger<OrderingOutboxDispatcher> logger) : BackgroundService
{
    public async Task TryDeliverAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PurchaseOrderingDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var messages = await db.OrderingOutbox.FromSqlRaw("SELECT * FROM purchase_ordering.ordering_outbox WHERE processed_at IS NULL ORDER BY created_at LIMIT 100 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
            if (messages.Count == 0) { await tx.CommitAsync(ct); return; }
            var supply = scope.ServiceProvider.GetRequiredService<SupplyRequestsDbContext>();
            var audit = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();
            supply.Database.SetDbConnection(db.Database.GetDbConnection(), contextOwnsConnection: false);
            audit.Database.SetDbConnection(db.Database.GetDbConnection(), contextOwnsConnection: false);
            await supply.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
            await audit.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
            var events = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            foreach (var message in messages)
            {
                IDomainEvent e = message.EventType switch {
                    nameof(PurchaseOrderIssued) => JsonSerializer.Deserialize<PurchaseOrderIssued>(message.Payload)!,
                    nameof(PurchaseRequestStatusChanged) => JsonSerializer.Deserialize<PurchaseRequestStatusChanged>(message.Payload)!,
                    _ => throw new InvalidOperationException("Unsupported ordering outbox event type.")
                };
                await events.DispatchAsync([e], ct); message.ProcessedAt = DateTimeOffset.UtcNow;
            }
            // Handler writes and acknowledgement share the transaction: retries cannot duplicate audit/notifications.
            await supply.SaveChangesAsync(ct); await audit.SaveChangesAsync(ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogError(ex, "Award events remain pending; outbox delivery will retry."); }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await TryDeliverAsync(stoppingToken);
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
