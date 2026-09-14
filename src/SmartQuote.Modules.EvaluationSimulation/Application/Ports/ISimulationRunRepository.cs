using SmartQuote.API.Shared.Domain.Repositories;
using SmartQuote.API.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.API.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.API.EvaluationSimulation.Application.Ports;

public interface ISimulationRunRepository : IBaseRepository<SimulationRun>
{
    Task<SimulationRun?> GetByIdAsync(SimulationRunId runId, CancellationToken cancellationToken = default);

    Task<SimulationRun?> FindByFingerprintAsync(InputFingerprint fingerprint, CancellationToken cancellationToken = default);
}
