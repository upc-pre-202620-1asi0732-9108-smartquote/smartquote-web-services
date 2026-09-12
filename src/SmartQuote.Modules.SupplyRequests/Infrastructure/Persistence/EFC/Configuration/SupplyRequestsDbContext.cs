using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Infrastructure.Notifications;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;

public sealed class SupplyRequestsDbContext(DbContextOptions<SupplyRequestsDbContext> options) : DbContext(options)
{
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<RequestNotificationRecord> RequestNotifications => Set<RequestNotificationRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplySupplyRequestsConfiguration();
        builder.ApplyRequestNotificationsConfiguration();
        builder.UseSnakeCaseNamingConvention();
    }
}
