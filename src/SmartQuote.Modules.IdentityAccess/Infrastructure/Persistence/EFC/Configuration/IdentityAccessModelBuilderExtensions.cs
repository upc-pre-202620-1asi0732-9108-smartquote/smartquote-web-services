using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Entities;

namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Configuration;

public static class IdentityAccessModelBuilderExtensions
{
    private const string Schema = "identity_access";

    public static void ApplyIdentityAccessConfiguration(this ModelBuilder builder)
    {
        builder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("user_accounts", Schema);
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Id)
                .HasConversion(id => id.Value, value => new UserId(value))
                .ValueGeneratedNever();
            entity.Property(account => account.Email).IsRequired().HasMaxLength(254);
            entity.Property(account => account.NormalizedEmail).IsRequired().HasMaxLength(254);
            entity.HasIndex(account => account.NormalizedEmail).IsUnique();
            entity.Property(account => account.DisplayName).IsRequired().HasMaxLength(150);
            entity.Property(account => account.PasswordHash).IsRequired().HasMaxLength(512);
            entity.Property(account => account.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(account => account.CreatedAt).IsRequired();
            entity.Property(account => account.UpdatedAt).IsRequired();

            entity.OwnsMany(account => account.Roles, role =>
            {
                role.ToTable("user_roles", Schema);
                role.WithOwner().HasForeignKey("user_account_id");
                role.HasKey(item => item.Id);
                role.Property(item => item.Id).ValueGeneratedNever();
                role.Property(item => item.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
                role.HasIndex("user_account_id", nameof(UserRole.Role)).IsUnique();
            });

            entity.OwnsMany(account => account.RefreshSessions, session =>
            {
                session.ToTable("refresh_sessions", Schema);
                session.WithOwner().HasForeignKey("user_account_id");
                session.HasKey(item => item.Id);
                session.Property(item => item.Id).ValueGeneratedNever();
                session.Property(item => item.TokenHash).IsRequired().HasMaxLength(64);
                session.HasIndex(item => item.TokenHash).IsUnique();
                session.Property(item => item.ExpiresAt).IsRequired();
                session.Property(item => item.CreatedAt).IsRequired();
                session.Property(item => item.RevokedAt);
            });

            entity.Navigation(account => account.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(account => account.RefreshSessions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
