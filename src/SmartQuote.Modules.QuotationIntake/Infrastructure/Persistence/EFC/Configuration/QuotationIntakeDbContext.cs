using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Configuration;

public sealed class QuotationIntakeDbContext(DbContextOptions<QuotationIntakeDbContext> options) : DbContext(options)
{
    public DbSet<PoultryQuote> PoultryQuotes => Set<PoultryQuote>();
    public DbSet<QuotationExpiryAlert> QuotationExpiryAlerts => Set<QuotationExpiryAlert>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyQuotationIntakeConfiguration();
        builder.Entity<QuotationExpiryAlert>(e => {
            e.ToTable("quotation_expiry_alerts", "quotation_intake"); e.HasKey(a => a.Id);
            e.Property(a => a.SupplierName).HasMaxLength(200);
            e.HasIndex(a => new { a.QuotationId, a.ValidUntil, a.LeadDays }).IsUnique();
            e.HasIndex(a => new { a.RecipientId, a.ReadAt });
        });
        builder.UseSnakeCaseNamingConvention();
    }
}
