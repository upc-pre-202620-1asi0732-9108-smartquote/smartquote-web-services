using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

public static class PurchaseOrderModelBuilderExtensions
{
    private const string Schema = "purchase_ordering";

    public static void ApplyPurchaseOrderingConfiguration(this ModelBuilder builder)
    {
        builder.HasSequence<long>("order_number_seq", Schema).StartsAt(1).IncrementsBy(1);

        builder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("purchase_orders", Schema);

            entity.HasKey(order => order.Id);
            entity.Property(order => order.Id)
                .HasConversion(id => id.Value, value => new PurchaseOrderId(value))
                .ValueGeneratedNever();

            entity.Property(order => order.OrderNumber)
                .HasColumnName("order_number")
                .HasConversion(number => number.Value, value => new OrderNumber(value))
                .HasMaxLength(40)
                .IsRequired();

            entity.HasIndex(order => order.OrderNumber).IsUnique();

            entity.OwnsOne(order => order.SourceDecision, decision =>
            {
                decision.WithOwner().HasForeignKey("Id");

                decision.Property(d => d.SimulationRunId)
                    .HasColumnName("source_simulation_run_id")
                    .HasConversion(id => Guid.Parse(id), value => value.ToString())
                    .HasColumnType("uuid")
                    .IsRequired();

                decision.Property(d => d.PurchaseRequestId)
                    .HasColumnName("source_purchase_request_id")
                    .HasConversion(id => Guid.Parse(id), value => value.ToString())
                    .HasColumnType("uuid")
                    .IsRequired();

                decision.Property(d => d.QuotationId)
                    .HasColumnName("source_quotation_id")
                    .HasConversion(id => Guid.Parse(id), value => value.ToString())
                    .HasColumnType("uuid")
                    .IsRequired();

                decision.Property(d => d.InputFingerprint).HasColumnName("input_fingerprint").HasColumnType("char(64)").IsRequired();

                decision.HasIndex(d => d.SimulationRunId).IsUnique();
            });

            entity.OwnsOne(order => order.Supplier, supplier =>
            {
                supplier.WithOwner().HasForeignKey("Id");
                supplier.Property(s => s.SupplierId).HasColumnName("supplier_reference_id").IsRequired().HasMaxLength(100);
                supplier.Property(s => s.BusinessName).HasColumnName("supplier_business_name").IsRequired().HasMaxLength(200);
                supplier.Property(s => s.TaxIdentifier).HasColumnName("supplier_tax_identifier").IsRequired().HasMaxLength(20);
            });

            entity.OwnsOne(order => order.Approval, approval =>
            {
                approval.WithOwner().HasForeignKey("Id");

                approval.Property(a => a.ApprovedBy)
                    .HasColumnName("approved_by")
                    .HasConversion(userId => userId.Value, value => new UserId(value))
                    .IsRequired();

                approval.Property(a => a.ApprovedAt).HasColumnName("approved_at").IsRequired();
                approval.Property(a => a.IdempotencyKey).HasColumnName("idempotency_key").IsRequired().HasMaxLength(100);

                approval.HasIndex(a => a.IdempotencyKey).IsUnique();
            });

            entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(order => order.Currency).HasColumnType("char(3)").IsRequired();

            entity.OwnsOne(order => order.DeliveryTerms, terms =>
            {
                terms.WithOwner().HasForeignKey("Id");
                terms.Property(t => t.LeadTimeDays).HasColumnName("delivery_lead_time_days").IsRequired();
                terms.Property(t => t.Conditions).HasColumnName("delivery_conditions");
                terms.Property(t => t.Destination).HasColumnName("delivery_destination").HasMaxLength(250);
            });

            entity.Property(order => order.CreatedAt).IsRequired();

            entity.OwnsMany(order => order.Lines, lines =>
            {
                lines.ToTable("purchase_order_lines", Schema);
                lines.WithOwner().HasForeignKey("purchase_order_id");
                lines.HasKey(line => line.Id);

                lines.Property(line => line.Id)
                    .HasConversion(id => id.Value, value => new PurchaseOrderLineId(value))
                    .ValueGeneratedNever();

                lines.Property(line => line.LineNumber).IsRequired();

                lines.Property(line => line.SourceQuotationLineId)
                    .HasColumnName("source_quotation_line_id")
                    .HasConversion(id => id == null ? (Guid?)null : Guid.Parse(id), value => value == null ? null : value.ToString())
                    .HasColumnType("uuid");

                lines.Property(line => line.SourceRequestedItemId)
                    .HasColumnName("source_requested_item_id")
                    .HasConversion(id => id == null ? (Guid?)null : Guid.Parse(id), value => value == null ? null : value.ToString())
                    .HasColumnType("uuid");

                lines.Property(line => line.Description).IsRequired().HasMaxLength(250);
                lines.Property(line => line.Quantity).HasColumnType("numeric(18,4)").IsRequired();
                lines.Property(line => line.UnitOfMeasure).IsRequired().HasMaxLength(30);
                lines.Property(line => line.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();
            });

            entity.Navigation(order => order.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
