using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;
using SmartQuote.API.SupplyRequests.Domain.Model.Entities;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;

public static class PurchaseRequestModelBuilderExtensions
{
    private const string Schema = "supply_requests";

    public static void ApplySupplyRequestsConfiguration(this ModelBuilder builder)
    {
        builder.Entity<PurchaseRequest>(entity =>
        {
            entity.ToTable("purchase_requests", Schema);

            entity.HasKey(request => request.Id);
            entity.Property(request => request.Id)
                .HasConversion(id => id.Value, value => new PurchaseRequestId(value))
                .ValueGeneratedNever();

            entity.Property(request => request.RequesterId)
                .HasConversion(userId => userId.Value, value => new UserId(value))
                .IsRequired();

            entity.Property(request => request.RequiredDate).IsRequired();

            entity.Property(request => request.Priority)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(request => request.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(request => request.Version).IsRequired().IsConcurrencyToken();
            entity.Property(request => request.CreatedAt).IsRequired();
            entity.Property(request => request.UpdatedAt).IsRequired();

            entity.OwnsMany(request => request.Items, itemsBuilder =>
            {
                itemsBuilder.ToTable("requested_items", Schema);
                itemsBuilder.WithOwner().HasForeignKey("purchase_request_id");
                itemsBuilder.HasKey(item => item.Id);

                itemsBuilder.Property(item => item.Id)
                    .HasConversion(id => id.Value, value => new RequestedItemId(value))
                    .ValueGeneratedNever();

                itemsBuilder.Property(item => item.LineNumber).IsRequired();
                itemsBuilder.Property(item => item.Description).IsRequired().HasMaxLength(250);
                itemsBuilder.Property(item => item.Quantity).HasColumnType("numeric(18,4)").IsRequired();
                itemsBuilder.Property(item => item.UnitOfMeasure).IsRequired().HasMaxLength(30);

                itemsBuilder.HasIndex("purchase_request_id", nameof(RequestedItem.LineNumber)).IsUnique();

                itemsBuilder.OwnsMany(item => item.Requirements, requirementsBuilder =>
                {
                    requirementsBuilder.ToTable("technical_requirements", Schema);
                    requirementsBuilder.WithOwner().HasForeignKey("requested_item_id");
                    requirementsBuilder.Property("requested_item_id").IsRequired();
                    requirementsBuilder.HasKey(requirement => requirement.Id);
                    requirementsBuilder.Property(requirement => requirement.Id)
                        .HasConversion(id => id.Value, value => new TechnicalRequirementId(value))
                        .ValueGeneratedNever();

                    requirementsBuilder.Property(requirement => requirement.Name).IsRequired().HasMaxLength(150);
                    requirementsBuilder.Property(requirement => requirement.Operator)
                        .HasConversion<string>()
                        .HasColumnName("comparison_operator")
                        .HasMaxLength(30)
                        .IsRequired();
                    requirementsBuilder.Property(requirement => requirement.ExpectedValue).IsRequired().HasMaxLength(250);
                    requirementsBuilder.Property(requirement => requirement.UnitOfMeasure).HasMaxLength(30);
                    requirementsBuilder.Property(requirement => requirement.IsMandatory).IsRequired();
                });

                itemsBuilder.Navigation(item => item.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            entity.OwnsMany(request => request.Attachments, attachmentsBuilder =>
            {
                attachmentsBuilder.ToTable("request_attachments", Schema);
                attachmentsBuilder.WithOwner().HasForeignKey("purchase_request_id");
                attachmentsBuilder.HasKey(attachment => attachment.Id);

                attachmentsBuilder.Property(attachment => attachment.Id)
                    .HasConversion(id => id.Value, value => new RequestAttachmentId(value))
                    .ValueGeneratedNever();

                attachmentsBuilder.Property(attachment => attachment.FileName).IsRequired().HasMaxLength(255);
                attachmentsBuilder.Property(attachment => attachment.ContentType).IsRequired().HasMaxLength(100);
                attachmentsBuilder.Property(attachment => attachment.StorageKey).IsRequired().HasMaxLength(500);

                attachmentsBuilder.Property(attachment => attachment.UploadedBy)
                    .HasConversion(userId => userId.Value, value => new UserId(value))
                    .IsRequired();

                attachmentsBuilder.Property(attachment => attachment.UploadedAt).IsRequired();
            });

            entity.OwnsMany(request => request.StatusHistory, historyBuilder =>
            {
                historyBuilder.ToTable("request_status_history", Schema);
                historyBuilder.WithOwner().HasForeignKey("purchase_request_id");
                historyBuilder.Property("purchase_request_id").IsRequired();
                historyBuilder.Property<Guid>("id").ValueGeneratedOnAdd();
                historyBuilder.HasKey("id");

                historyBuilder.Property(entry => entry.FromStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
                historyBuilder.Property(entry => entry.ToStatus).HasConversion<string>().HasMaxLength(30).IsRequired();

                historyBuilder.Property(entry => entry.ChangedBy)
                    .HasConversion(userId => userId.Value, value => new UserId(value))
                    .IsRequired();

                historyBuilder.Property(entry => entry.ChangedAt).IsRequired();
                historyBuilder.Property(entry => entry.Reason).IsRequired();
            });

            entity.Navigation(request => request.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(request => request.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(request => request.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
