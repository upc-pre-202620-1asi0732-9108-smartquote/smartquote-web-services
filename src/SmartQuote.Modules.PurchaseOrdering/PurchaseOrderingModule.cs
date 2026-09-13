using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartQuote.API.PurchaseOrdering.Application;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Domain.Services;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Repositories;

namespace SmartQuote.API.PurchaseOrdering;

public static class PurchaseOrderingModule
{
    public static IServiceCollection AddPurchaseOrderingModule(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddDbContext<PurchaseOrderingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "purchase_ordering")));

        services.AddScoped<IPurchaseOrderingUnitOfWork, PurchaseOrderingUnitOfWork>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IOrderNumberGenerator, SequentialOrderNumberGenerator>();
        services.AddScoped<ApprovedDecisionMapper>();
        services.AddScoped<PurchaseOrderGenerator>();
        services.AddScoped<PurchaseOrderApplicationService>();

        return services;
    }
}
