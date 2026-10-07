using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;

public sealed class EvaluationSimulationDbContext(DbContextOptions<EvaluationSimulationDbContext> options) : DbContext(options)
{
    public DbSet<EvaluationScenario> EvaluationScenarios => Set<EvaluationScenario>();
    public DbSet<SimulationRun> SimulationRuns => Set<SimulationRun>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyEvaluationSimulationConfiguration();
        builder.UseSnakeCaseNamingConvention();
    }
}
