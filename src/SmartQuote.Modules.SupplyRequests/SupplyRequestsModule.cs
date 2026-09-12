using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Application;
using SmartQuote.API.SupplyRequests.Application.EventHandlers;
using SmartQuote.API.SupplyRequests.Application.OutboundServices;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Domain.Model.Events;
using SmartQuote.API.SupplyRequests.Infrastructure.Notifications;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Repositories;
using SmartQuote.API.SupplyRequests.Infrastructure.Storage;

namespace SmartQuote.API.SupplyRequests;

public static class SupplyRequestsModule
{
    public static IServiceCollection AddSupplyRequestsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddDbContext<SupplyRequestsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "supply_requests")));

        services.AddScoped<ISupplyRequestsUnitOfWork, SupplyRequestsUnitOfWork>();
        services.AddScoped<IPurchaseRequestRepository, PurchaseRequestRepository>();
        services.AddScoped<IRequestNotificationPort, InAppRequestNotificationAdapter>();
        services.AddScoped<IRequestNotificationReader>(provider =>
            provider.GetRequiredService<IRequestNotificationPort>() as InAppRequestNotificationAdapter
            ?? throw new InvalidOperationException("The in-app notification adapter is not registered."));
        services.AddScoped<IRequestAttachmentStorage, LocalRequestAttachmentStorage>();
        services.AddScoped<PurchaseRequestCommandService>();
        services.AddScoped<PurchaseRequestQueryService>();
        services.AddScoped<RequestNotificationService>();
        services.AddScoped<IPurchaseRequestSnapshotProvider>(provider =>
            provider.GetRequiredService<PurchaseRequestQueryService>());
        services.AddScoped<IDomainEventHandler<PurchaseRequestStatusChanged>, RequestStatusChangedNotificationHandler>();

        return services;
    }
}
