using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Application;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Application;

public class SimulationValidityService(
    IEvaluationScenarioRepository scenarioRepository,
    IPurchaseRequestSnapshotReader requestSnapshotReader,
    IVerifiedQuotationSnapshotReader quotationSnapshotReader,
    EvaluationInputAssembler inputAssembler)
{
    public async Task EnsureCurrentAsync(SimulationRun simulationRun, CancellationToken cancellationToken = default)
    {
        var currentFingerprint = await CalculateCurrentFingerprintAsync(simulationRun, cancellationToken);

        if (!simulationRun.IsBasedOn(currentFingerprint))
            throw new DomainException($"Simulation run '{simulationRun.Id}' is no longer based on the current request and quotations; run a new simulation.");
    }

    public async Task<InputFingerprint> CalculateCurrentFingerprintAsync(SimulationRun simulationRun, CancellationToken cancellationToken = default)
    {
        var scenario = await scenarioRepository.GetByIdAsync(simulationRun.ScenarioId, cancellationToken)
            ?? throw new NotFoundException($"Evaluation scenario '{simulationRun.ScenarioId}' was not found.");

        var requestSnapshot = await requestSnapshotReader.GetCurrentAsync(scenario.RequestId, cancellationToken)
            ?? throw new NotFoundException($"Purchase request '{scenario.RequestId}' was not found.");

        var quotationSnapshots = await quotationSnapshotReader.GetCurrentForRequestAsync(scenario.RequestId, cancellationToken);

        var dataset = inputAssembler.Assemble(requestSnapshot, quotationSnapshots);

        return InputFingerprint.FromParts(dataset.CalculateFingerprint().Value, scenario.CalculateDefinitionFingerprint().Value);
    }
}
