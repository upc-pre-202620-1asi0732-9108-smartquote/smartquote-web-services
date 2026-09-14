

using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Repositories;

public class SimulationRunRepository(EvaluationSimulationDbContext context)
    : BaseRepository<SimulationRun, EvaluationSimulationDbContext>(context), ISimulationRunRepository
{
    public async Task<SimulationRun?> GetByIdAsync(SimulationRunId runId, CancellationToken cancellationToken = default) =>
        await Context.SimulationRuns
            .AsSplitQuery()
            .Include(run => run.Evaluations)
            .Include(run => run.RequestSnapshot!.Items)
            .ThenInclude(item => item.Requirements)
            .Include(run => run.QuotationSnapshots)
            .ThenInclude(snapshot => snapshot.Lines)
            .ThenInclude(line => line.Specifications)
            .FirstOrDefaultAsync(run => run.Id == runId, cancellationToken);

    public async Task<SimulationRun?> FindByFingerprintAsync(InputFingerprint fingerprint, CancellationToken cancellationToken = default) =>
        await Context.SimulationRuns
            .Where(run => run.InputFingerprint == fingerprint)
            .FirstOrDefaultAsync(cancellationToken);
}
