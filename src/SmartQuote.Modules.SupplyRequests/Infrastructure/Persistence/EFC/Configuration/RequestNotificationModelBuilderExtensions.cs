using Microsoft.EntityFrameworkCore;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Infrastructure.Notifications;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;

public static class RequestNotificationModelBuilderExtensions
{
    private const string Schema = "supply_requests";

    public static void ApplyRequestNotificationsConfiguration(this ModelBuilder builder)
    {
        builder.Entity<RequestNotificationRecord>(entity =>
        {
            entity.ToTable("request_notifications", Schema);

            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.Id).ValueGeneratedNever();

            entity.Property(notification => notification.PurchaseRequestId)
                .HasConversion(id => id.Value, value => new PurchaseRequestId(value))
                .IsRequired();

            entity.Property(notification => notification.RecipientId).IsRequired();
            entity.Property(notification => notification.NewStatus).IsRequired().HasMaxLength(30);
            entity.Property(notification => notification.Message).IsRequired();
            entity.Property(notification => notification.CreatedAt).IsRequired();
            entity.Property(notification => notification.ReadAt);

            entity.HasOne<PurchaseRequest>()
                .WithMany()
                .HasForeignKey(notification => notification.PurchaseRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
