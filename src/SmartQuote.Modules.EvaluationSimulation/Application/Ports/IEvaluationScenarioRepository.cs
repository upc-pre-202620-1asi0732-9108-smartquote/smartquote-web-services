using SmartQuote.API.Shared.Domain.Repositories;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Application.Ports;

public interface IEvaluationScenarioRepository : IBaseRepository<EvaluationScenario>
{
    Task<EvaluationScenario?> GetByIdAsync(EvaluationScenarioId scenarioId, CancellationToken cancellationToken = default);

    Task<EvaluationScenario?> GetCurrentForRequestAsync(string requestId, CancellationToken cancellationToken = default);
}
