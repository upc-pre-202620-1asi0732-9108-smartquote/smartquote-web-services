using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Events;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Domain.Model.Events;
using SmartQuote.Modules.Auditing.Application;
using SmartQuote.Modules.Auditing.Application.EventHandlers;
using SmartQuote.Modules.Auditing.Infrastructure.Persistence.EFC;

namespace SmartQuote.Modules.Auditing;

public static class AuditingModule
{
    public static IServiceCollection AddAuditingModule(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddDbContext<AuditingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "auditing")));

        services.AddScoped<AuditTrailService>();
        services.AddScoped<IDomainEventHandler<PurchaseRequestSubmitted>, AuditRequestSubmittedHandler>();
        services.AddScoped<IDomainEventHandler<PurchaseRequestStatusChanged>, AuditRequestStatusChangedHandler>();
        services.AddScoped<IDomainEventHandler<PurchaseOrderIssued>, AuditOrderIssuedHandler>();
        services.AddScoped<IDomainEventHandler<PurchaseOrderDelivered>, AuditOrderDeliveredHandler>();

        return services;
    }
}
