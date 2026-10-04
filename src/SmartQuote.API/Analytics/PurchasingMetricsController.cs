using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Enums;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.API.Analytics;

[ApiController]
[Route("api/v1/purchasing-metrics")]
[Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
public sealed class PurchasingMetricsController(
    PurchaseOrderingDbContext orders,
    SupplyRequestsDbContext requests,
    EvaluationSimulationDbContext simulations) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PurchasingMetricsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PurchasingMetricsResponse>> Get(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (from is null || to is null || to < from || to == DateOnly.MaxValue)
        {
            ModelState.AddModelError("period", "Specify valid from and to dates (YYYY-MM-DD), with to on or after from.");
            return ValidationProblem(ModelState);
        }

        var start = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endExclusive = new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var periodOrders = await orders.PurchaseOrders.AsNoTracking()
            .Where(order => order.Status == PurchaseOrderStatus.Issued &&
                            order.CreatedAt >= start && order.CreatedAt < endExclusive)
            .ToListAsync(cancellationToken);

        decimal totalHours = 0;
        var timedCount = 0;
        decimal totalSavings = 0;
        var comparedCount = 0;

        foreach (var order in periodOrders)
        {
            if (!Guid.TryParse(order.SourceDecision.PurchaseRequestId, out var requestGuid) ||
                !Guid.TryParse(order.SourceDecision.SimulationRunId, out var runGuid))
                continue;

            var request = await requests.PurchaseRequests.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == new PurchaseRequestId(requestGuid), cancellationToken);
            if (request is not null && order.CreatedAt >= request.CreatedAt)
            {
                totalHours += (decimal)(order.CreatedAt - request.CreatedAt).TotalHours;
                timedCount++;
            }

            var run = await simulations.SimulationRuns.AsNoTracking().AsSplitQuery()
                .Include(item => item.Evaluations)
                .Include(item => item.QuotationSnapshots).ThenInclude(snapshot => snapshot.Lines)
                .FirstOrDefaultAsync(item => item.Id == new SimulationRunId(runGuid), cancellationToken);
            if (run is null || run.RequestSnapshot.RequestId != order.SourceDecision.PurchaseRequestId ||
                run.InputFingerprint.Value != order.SourceDecision.InputFingerprint)
                continue;

            var savings = PurchasingMetricsCalculator.ComparativeSavingsPen(run, order.SourceDecision.QuotationId);
            if (savings is null)
                continue;
            totalSavings += savings.Value;
            comparedCount++;
        }

        return Ok(new PurchasingMetricsResponse(
            from.Value, to.Value, "UTC", "Issued", periodOrders.Count,
            timedCount, timedCount == 0 ? null : decimal.Round(totalHours / timedCount, 2),
            comparedCount, comparedCount == 0 ? null : decimal.Round(totalSavings, 2), "PEN",
            "Average hours from purchase request creation to issued purchase order creation; only linked orders with valid dates are counted.",
            "Sum of max(0, cheapest other eligible quotation minus selected quotation) for each issued order, in PEN using the simulation's stored exchange rate. This is an estimated comparison, not realized financial savings; orders without two comparable eligible quotations are excluded."));
    }
}

public sealed record PurchasingMetricsResponse(
    DateOnly From,
    DateOnly To,
    string TimeZone,
    string OrderStatus,
    int OrderCount,
    int TimeSampleCount,
    decimal? AverageProcessingHours,
    int SavingsSampleCount,
    decimal? ComparativeSavings,
    string SavingsCurrency,
    string TimeDefinition,
    string SavingsDefinition);
