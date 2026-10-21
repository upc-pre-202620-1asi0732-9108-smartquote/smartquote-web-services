using Microsoft.EntityFrameworkCore;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.Shared.Infrastructure.Persistence;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

public sealed class PurchaseOrderingDbContext(DbContextOptions<PurchaseOrderingDbContext> options) : DbContext(options)
{
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<DeliveryEvaluation> DeliveryEvaluations => Set<DeliveryEvaluation>();
    public DbSet<SupplierAccount> SupplierAccounts => Set<SupplierAccount>();
    public DbSet<PurchaseBudget> PurchaseBudgets => Set<PurchaseBudget>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyPurchaseOrderingConfiguration();
        builder.ApplyDeliveryEvaluationConfiguration();
        builder.ApplyOrderingPolicyConfiguration();
        builder.UseSnakeCaseNamingConvention();
    }
}
