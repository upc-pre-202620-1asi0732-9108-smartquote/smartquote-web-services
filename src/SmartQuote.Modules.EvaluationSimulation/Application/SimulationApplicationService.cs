using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Application.Views;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Services;

namespace SmartQuote.Modules.EvaluationSimulation.Application;

public class SimulationApplicationService(
    IEvaluationScenarioRepository scenarioRepository,
    ISimulationRunRepository runRepository,
    IPurchaseRequestSnapshotReader requestSnapshotReader,
    IVerifiedQuotationSnapshotReader quotationSnapshotReader,
    EvaluationInputAssembler inputAssembler,
    SimulationEngine simulationEngine,
    SimulationValidityService validityService,
    IEvaluationSimulationUnitOfWork unitOfWork,
    IDomainEventDispatcher domainEventDispatcher) : ISimulationDecisionReader
{
    public async Task<SimulationRunExecutionResult> RunAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var scenario = await scenarioRepository.GetByIdAsync(new EvaluationScenarioId(scenarioId), cancellationToken)
            ?? throw new NotFoundException($"Evaluation scenario '{scenarioId}' was not found.");

        if (scenario.Status != ScenarioStatus.Active)
            throw new DomainException($"Evaluation scenario '{scenarioId}' is not active.");

        var requestSnapshot = await requestSnapshotReader.GetCurrentAsync(scenario.RequestId, cancellationToken)
            ?? throw new NotFoundException($"Purchase request '{scenario.RequestId}' was not found.");

        var quotationSnapshots = await quotationSnapshotReader.GetCurrentForRequestAsync(scenario.RequestId, cancellationToken);

        var dataset = inputAssembler.Assemble(requestSnapshot, quotationSnapshots);

        if (!requestSnapshot.Status.Equals("Evaluation", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("The purchase request must be in Evaluation status before running a simulation.");

        var fingerprint = InputFingerprint.FromParts(dataset.CalculateFingerprint().Value, scenario.CalculateDefinitionFingerprint().Value);
        var existingRun = await runRepository.FindByFingerprintAsync(fingerprint, cancellationToken);
        if (existingRun is not null)
            return new SimulationRunExecutionResult(existingRun.Id, false);

        var run = simulationEngine.Run(scenario, dataset);

        await runRepository.AddAsync(run, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);

        await domainEventDispatcher.DispatchAsync(run.DomainEvents, cancellationToken);
        run.ClearDomainEvents();

        return new SimulationRunExecutionResult(run.Id, true);
    }

    public async Task<SimulationResultView> GetResultAsync(Guid simulationRunId, CancellationToken cancellationToken = default)
    {
        var run = await FindOrThrowAsync(simulationRunId, cancellationToken);

        var currentFingerprint = await validityService.CalculateCurrentFingerprintAsync(run, cancellationToken);
        var isCurrent = run.IsBasedOn(currentFingerprint);

        return ToView(run, isCurrent);
    }

    public async Task<ApprovedSimulationSnapshot?> GetApprovedSnapshotAsync(Guid runId, string quotationId, CancellationToken cancellationToken = default)
    {
        var run = await FindOrThrowAsync(runId, cancellationToken);

        await validityService.EnsureCurrentAsync(run, cancellationToken);

        var evaluation = run.GetEvaluation(quotationId);
        if (!evaluation.IsEligible)
            throw new DomainException($"Quotation '{quotationId}' is not eligible in simulation run '{runId}'.");

        var quotationSnapshot = run.QuotationSnapshots.First(snapshot => snapshot.QuotationId == quotationId);

        return new ApprovedSimulationSnapshot(
            run.Id.ToString(),
            run.RequestSnapshot.RequestId,
            quotationId,
            quotationSnapshot.SupplierId,
            quotationSnapshot.SupplierBusinessName,
            quotationSnapshot.SupplierTaxIdentifier,
            quotationSnapshot.Currency,
            quotationSnapshot.Lines.Select(line => new ApprovedOrderLineSnapshot(
                line.SourceQuotationLineId,
                line.SourceRequestedItemId,
                line.Description,
                line.Quantity,
                line.UnitOfMeasure,
                line.UnitPrice)).ToList(),
            quotationSnapshot.DeliveryLeadTimeDays,
            run.InputFingerprint.Value,
            true);
    }

    private async Task<SimulationRun> FindOrThrowAsync(Guid runId, CancellationToken cancellationToken) =>
        await runRepository.GetByIdAsync(new SimulationRunId(runId), cancellationToken)
            ?? throw new NotFoundException($"Simulation run '{runId}' was not found.");

    private static SimulationResultView ToView(SimulationRun run, bool isCurrent) => new(
        run.Id,
        run.ScenarioId,
        run.CriteriaVersion,
        run.InputFingerprint.Value,
        run.ExecutedAt,
        isCurrent,
        run.Recommendation is null ? null : new RecommendationView(run.Recommendation.QuotationId, run.Recommendation.Score.Value, run.Recommendation.Explanation),
        run.Evaluations.Select(evaluation => new QuotationEvaluationView(
            evaluation.QuotationId,
            evaluation.IsEligible,
            evaluation.TotalScore.Value,
            evaluation.Rank,
            evaluation.CriterionResults.Select(result => new CriterionResultView(result.CriterionId, result.Passed, result.NormalizedScore, result.WeightedContribution, result.Explanation)).ToList(),
            evaluation.ExclusionReasons.Select(reason => new ExclusionReasonView(reason.CriterionId, reason.Code, reason.Explanation)).ToList())).ToList());
}
