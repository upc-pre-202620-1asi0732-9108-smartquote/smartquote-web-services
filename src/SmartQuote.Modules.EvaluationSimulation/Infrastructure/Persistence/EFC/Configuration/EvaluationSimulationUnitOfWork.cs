using SmartQuote.Modules.EvaluationSimulation.Application.Ports;

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;

public sealed class EvaluationSimulationUnitOfWork(EvaluationSimulationDbContext context) : IEvaluationSimulationUnitOfWork
{
    public Task CompleteAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
