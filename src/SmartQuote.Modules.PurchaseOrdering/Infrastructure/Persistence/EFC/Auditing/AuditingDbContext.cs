using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Entities;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Auditing;

public sealed class AuditingDbContext(DbContextOptions<AuditingDbContext> options) : DbContext(options)
{
    private const string Schema = "auditing";

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events", Schema);

            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Id).ValueGeneratedNever();

            entity.Property(audit => audit.EntityType).HasMaxLength(40).IsRequired();
            entity.Property(audit => audit.EntityId).IsRequired();
            entity.Property(audit => audit.Action).HasMaxLength(120).IsRequired();
            entity.Property(audit => audit.ActorId).IsRequired();
            entity.Property(audit => audit.Reason).HasMaxLength(500);
            entity.Property(audit => audit.OccurredAt).IsRequired();

            entity.HasIndex(audit => new { audit.EntityType, audit.EntityId, audit.OccurredAt });
        });

        builder.UseSnakeCaseNamingConvention();
    }
}
