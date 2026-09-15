using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;

namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Configuration;

public sealed class IdentityAccessDbContext(DbContextOptions<IdentityAccessDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyIdentityAccessConfiguration();
        builder.UseSnakeCaseNamingConvention();
    }
}
