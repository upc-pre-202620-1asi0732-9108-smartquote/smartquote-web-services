using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Configuration;

public sealed class QuotationIntakeDbContext(DbContextOptions<QuotationIntakeDbContext> options) : DbContext(options)
{
    public DbSet<PoultryQuote> PoultryQuotes => Set<PoultryQuote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyQuotationIntakeConfiguration();
        builder.UseSnakeCaseNamingConvention();
    }
}
