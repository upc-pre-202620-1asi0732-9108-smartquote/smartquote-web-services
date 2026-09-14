using SmartQuote.API.Shared.Domain.Repositories;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Application.Ports;

public interface ISimulationRunRepository : IBaseRepository<SimulationRun>
{
    Task<SimulationRun?> GetByIdAsync(SimulationRunId runId, CancellationToken cancellationToken = default);

    Task<SimulationRun?> FindByFingerprintAsync(InputFingerprint fingerprint, CancellationToken cancellationToken = default);
}
