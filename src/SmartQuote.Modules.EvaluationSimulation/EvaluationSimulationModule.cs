using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartQuote.Modules.EvaluationSimulation.Application;
using SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Domain.Services;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Repositories;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.ReferenceReaders;

namespace SmartQuote.Modules.EvaluationSimulation;

public static class EvaluationSimulationModule
{
    public static IServiceCollection AddEvaluationSimulationModule(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddDbContext<EvaluationSimulationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "evaluation_simulation")));

        services.AddScoped<IEvaluationSimulationUnitOfWork, EvaluationSimulationUnitOfWork>();
        services.AddScoped<IEvaluationScenarioRepository, EvaluationScenarioRepository>();
        services.AddScoped<ISimulationRunRepository, SimulationRunRepository>();
        services.AddScoped<IPurchaseRequestSnapshotReader, SupplyRequestSnapshotAdapter>();
        services.AddScoped<IVerifiedQuotationSnapshotReader, VerifiedQuotationSnapshotAdapter>();
        services.AddScoped<EvaluationInputAssembler>();
        services.AddScoped<SimulationEngine>();
        services.AddScoped<SimulationValidityService>();
        services.AddScoped<ScenarioApplicationService>();
        services.AddScoped<SimulationApplicationService>();
        services.AddScoped<ISimulationDecisionReader>(provider =>
            provider.GetRequiredService<SimulationApplicationService>());

        return services;
    }
}
