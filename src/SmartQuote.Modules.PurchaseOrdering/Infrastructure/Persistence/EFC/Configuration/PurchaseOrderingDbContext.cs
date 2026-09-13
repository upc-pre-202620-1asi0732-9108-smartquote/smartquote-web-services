using Microsoft.EntityFrameworkCore;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.Shared.Infrastructure.Persistence;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

public sealed class PurchaseOrderingDbContext(DbContextOptions<PurchaseOrderingDbContext> options) : DbContext(options)
{
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyPurchaseOrderingConfiguration();
        builder.UseSnakeCaseNamingConvention();
    }
}
