

using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Repositories;

public class EvaluationScenarioRepository(EvaluationSimulationDbContext context)
    : BaseRepository<EvaluationScenario, EvaluationSimulationDbContext>(context), IEvaluationScenarioRepository
{
    public async Task<EvaluationScenario?> GetByIdAsync(EvaluationScenarioId scenarioId, CancellationToken cancellationToken = default) =>
        await Context.EvaluationScenarios
            .Include(scenario => scenario.Criteria)
            .FirstOrDefaultAsync(scenario => scenario.Id == scenarioId, cancellationToken);

    public async Task<EvaluationScenario?> GetCurrentForRequestAsync(string requestId, CancellationToken cancellationToken = default) =>
        await Context.EvaluationScenarios
            .Include(scenario => scenario.Criteria)
            .Where(scenario => scenario.RequestId == requestId)
            .OrderByDescending(scenario => scenario.Version)
            .FirstOrDefaultAsync(cancellationToken);
}
