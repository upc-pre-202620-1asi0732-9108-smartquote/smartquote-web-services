using SmartQuote.API.Shared.Domain.Repositories;
using SmartQuote.API.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.API.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.API.EvaluationSimulation.Application.Ports;

public interface IEvaluationScenarioRepository : IBaseRepository<EvaluationScenario>
{
    Task<EvaluationScenario?> GetByIdAsync(EvaluationScenarioId scenarioId, CancellationToken cancellationToken = default);

    Task<EvaluationScenario?> GetCurrentForRequestAsync(string requestId, CancellationToken cancellationToken = default);
}
