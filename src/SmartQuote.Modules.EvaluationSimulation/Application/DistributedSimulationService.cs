using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Application.Views;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Services;

namespace SmartQuote.Modules.EvaluationSimulation.Application;

public sealed class DistributedSimulationService(EvaluationPreparationService preparation, DistributedSimulationEngine engine,
    ISimulationRunRepository runs, IEvaluationSimulationUnitOfWork work, SimulationValidityService validity) : IDistributedDecisionReader
{
    public async Task<SimulationRunExecutionResult> RunAsync(Guid scenarioId, CancellationToken ct)
    {
        var p = await preparation.PrepareAsync(scenarioId, false, null, ct);
        var fingerprint = InputFingerprint.FromParts(p.Dataset.CalculateFingerprint(p.Rate, p.Today).Value, p.Scenario.CalculateDefinitionFingerprint().Value);
        var existing = await runs.FindByFingerprintAsync(InputFingerprint.FromParts(fingerprint.Value, "DistributedByItem"), ct);
        if (existing is not null) return new(existing.Id, false);
        var distribution = engine.Evaluate(p.Scenario, p.Dataset, p.Rate, p.Today);
        var run = SimulationRun.Create(p.Scenario.Id, p.Scenario.Version, fingerprint, p.Dataset.Request, p.Dataset.Quotations, p.Rate);
        run.AttachDistribution(distribution); await runs.AddAsync(run, ct); await work.CompleteAsync(ct);
        return new(run.Id, true);
    }
    public async Task<string> RequestIdAsync(Guid runId, CancellationToken ct) => (await Find(runId, ct)).RequestSnapshot.RequestId;
    public async Task<DistributedDecisionSnapshot> ReadCurrentAsync(Guid runId, CancellationToken ct)
    {
        var run = await Find(runId, ct); await validity.EnsureCurrentAsync(run, ct);
        if (!run.Distribution!.CanApprove) throw new DomainException("The distributed simulation has uncovered items and cannot be approved.");
        if (run.Distribution.Allocations.Count != run.RequestSnapshot.Items.Count || run.RequestSnapshot.Items.Any(i =>
            run.Distribution.Allocations.Count(a => a.RequestedItemId == i.SourceRequestedItemId && a.Quantity == i.Quantity && a.UnitOfMeasure == i.UnitOfMeasure) != 1) ||
            run.Distribution.TotalPen != run.Distribution.Allocations.Sum(a => a.AmountPen))
            throw new DomainException("The distribution does not cover the complete current request exactly once.");
        return new(run.Id.Value, run.RequestSnapshot.RequestId, run.InputFingerprint.Value,
            run.Distribution.Allocations.Select(a => new ApprovedDistributedAllocationSnapshot(a.RequestedItemId, a.QuotationId, a.QuotationLineId,
                a.SupplierAccountId, a.SupplierEligibilityVersion, a.SupplierBusinessName, a.SupplierTaxIdentifier, a.Description, a.Quantity,
                a.UnitOfMeasure, a.OriginalCurrency, a.OriginalUnitPrice, a.OriginalTotal, a.UnitPricePen, a.AmountPen, a.Score, a.DeliveryLeadTimeDays)).ToList(),
            run.Distribution.TotalPen, run.ExchangeRate is { } rate ? new ApprovedExchangeRateSnapshot(rate.SourceCurrency, rate.TargetCurrency,
                rate.Rate, rate.RateType, rate.PublishedOn, rate.Source, rate.RetrievedAt) : null);
    }
    private async Task<SimulationRun> Find(Guid id, CancellationToken ct)
    {
        var run = await runs.GetByIdAsync(new(id), ct) ?? throw new NotFoundException("Distributed simulation was not found.");
        if (run.Mode != "DistributedByItem" || run.Distribution is null) throw new DomainException("This route requires a distributed simulation.");
        return run;
    }
}
