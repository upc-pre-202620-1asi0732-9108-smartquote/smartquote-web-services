using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartQuote.API.PurchaseOrdering.Application;
using SmartQuote.API.PurchaseOrdering.Application.EventHandlers;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Events;
using SmartQuote.API.PurchaseOrdering.Domain.Services;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Auditing;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Repositories;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Domain.Model.Events;
using SmartQuote.Modules.PurchaseOrdering.Application;

namespace SmartQuote.Modules.PurchaseOrdering;

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
        services.AddScoped<IDeliveryEvaluationRepository, DeliveryEvaluationRepository>();
        services.AddScoped<IOrderNumberGenerator, SequentialOrderNumberGenerator>();
        services.AddScoped<ApprovedDecisionMapper>();
        services.AddScoped<PurchaseOrderGenerator>();
        services.AddScoped<PurchaseOrderApplicationService>();

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
