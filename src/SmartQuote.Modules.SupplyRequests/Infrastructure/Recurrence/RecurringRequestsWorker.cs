using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartQuote.API.SupplyRequests.Application;
using SmartQuote.API.SupplyRequests.Application.Ports;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Recurrence;

public sealed class RecurringRequestsOptions
{
    public bool Enabled { get; set; } = true;
    public int PollSeconds { get; set; } = 30;
    public int MaxSchedulesPerCycle { get; set; } = 20;
    public int MaxOccurrencesPerSchedule { get; set; } = 10;
}

public sealed class RecurringRequestsWorker(IServiceScopeFactory scopes, IOptions<RecurringRequestsOptions> options,
    TimeProvider clock, ILogger<RecurringRequestsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 5, 3600)), clock);
        do
        {
            try
            {
                IReadOnlyList<Guid> ids;
                using (var scope = scopes.CreateScope())
                    ids = await scope.ServiceProvider.GetRequiredService<ISupplyWorkflowStore>().GetDueScheduleIdsAsync(
                        clock.GetUtcNow(), Math.Clamp(options.Value.MaxSchedulesPerCycle, 1, 100), stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<RecurringOccurrenceProcessor>().ProcessAsync(id,
                            Math.Clamp(options.Value.MaxOccurrencesPerSchedule, 1, 100), stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Failed to process recurring schedule {ScheduleId}", id); }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Recurring request polling failed; will retry"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
