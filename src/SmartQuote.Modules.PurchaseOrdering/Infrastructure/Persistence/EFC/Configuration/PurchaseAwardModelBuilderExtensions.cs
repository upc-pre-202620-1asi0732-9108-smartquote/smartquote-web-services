using Microsoft.EntityFrameworkCore;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Entities;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

public static class PurchaseAwardModelBuilderExtensions
{
    public static void ApplyPurchaseAwardConfiguration(this ModelBuilder b)
    {
        b.Entity<PurchaseAward>(e => {
            e.ToTable("purchase_awards", "purchase_ordering"); e.HasKey(a => a.Id);
            e.HasIndex(a => a.RequestId).IsUnique(); e.HasIndex(a => a.SimulationRunId).IsUnique(); e.HasIndex(a => a.IdempotencyKey).IsUnique();
            e.Property(a => a.IdempotencyKey).HasMaxLength(100); e.Property(a => a.RequestHash).HasMaxLength(64); e.Property(a => a.InputFingerprint).HasMaxLength(64);
            e.Property(a => a.Mode).HasMaxLength(30); e.Property(a => a.TotalPen).HasPrecision(28, 12);
            e.Property(a => a.BudgetAmountPen).HasPrecision(28, 12); e.Property(a => a.BudgetMarginPen).HasPrecision(28, 12);
            e.Property(a => a.ExchangeRate).HasPrecision(18, 8);
            e.OwnsMany(a => a.Allocations, l => {
                l.ToTable("purchase_award_allocations", "purchase_ordering"); l.WithOwner().HasForeignKey("award_id");
                l.HasKey("award_id", nameof(AwardAllocation.RequestedItemId));
                l.Property(a => a.Quantity).HasPrecision(18, 4); l.Property(a => a.OriginalUnitPrice).HasPrecision(18, 4);
                l.Property(a => a.UnitPricePen).HasPrecision(28, 12); l.Property(a => a.AmountPen).HasPrecision(28, 12);
            });
            e.Navigation(a => a.Allocations).AutoInclude();
        });
        b.Entity<PurchaseOrder>(e => {
            e.HasOne<PurchaseAward>().WithMany().HasForeignKey(o => o.AwardId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(o => new { o.AwardId, o.AwardSupplierId }).IsUnique();
        });
        b.Entity<OrderingOutboxMessage>(e => {
            e.ToTable("ordering_outbox", "purchase_ordering"); e.HasKey(m => m.Id);
            e.Property(m => m.Payload).HasColumnType("jsonb"); e.Property(m => m.EventType).HasMaxLength(150); e.HasIndex(m => m.ProcessedAt);
        });
    }
}
