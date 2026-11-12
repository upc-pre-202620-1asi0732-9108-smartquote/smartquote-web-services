using Microsoft.EntityFrameworkCore;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

public static class OrderingPolicyModelBuilderExtensions
{
    public static void ApplyOrderingPolicyConfiguration(this ModelBuilder builder)
    {
        builder.Entity<SupplierAccount>(e =>
        {
            e.ToTable("supplier_accounts", "purchase_ordering"); e.HasKey(s => s.Id);
            e.Property(s => s.Id).ValueGeneratedNever(); e.Property(s => s.Version).IsConcurrencyToken();
            e.Property(s => s.TaxIdentifier).HasMaxLength(11); e.HasIndex(s => s.TaxIdentifier).IsUnique();
            e.Property(s => s.BusinessName).HasMaxLength(200); e.Property(s => s.Status).HasMaxLength(20);
            e.OwnsMany(s => s.History, h =>
            {
                h.ToTable("supplier_eligibility_history", "purchase_ordering"); h.WithOwner().HasForeignKey("supplier_id");
                h.HasKey(x => x.Id); h.Property(x => x.Id).ValueGeneratedNever(); h.Property(x => x.Reason).HasMaxLength(2000);
                h.HasIndex("supplier_id", "Version").IsUnique();
            });
            e.Navigation(s => s.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<PurchaseBudget>(e =>
        {
            e.ToTable("purchase_budgets", "purchase_ordering"); e.HasKey(b => b.RequestId);
            e.Property(b => b.RequestId).ValueGeneratedNever(); e.Property(b => b.Version).IsConcurrencyToken();
            e.Property(b => b.AmountPen).HasColumnType("numeric(18,4)");
            e.OwnsMany(b => b.Revisions, h =>
            {
                h.ToTable("purchase_budget_revisions", "purchase_ordering"); h.WithOwner().HasForeignKey("request_id");
                h.HasKey(x => x.Id); h.Property(x => x.Id).ValueGeneratedNever(); h.Property(x => x.AmountPen).HasColumnType("numeric(18,4)");
                h.Property(x => x.Reason).HasMaxLength(2000); h.HasIndex("request_id", "Version").IsUnique();
            });
            e.Navigation(b => b.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<PurchaseOrder>().OwnsOne(o => o.PolicyEvidence, e =>
        {
            e.WithOwner().HasForeignKey("Id");
            e.Property(x => x.SupplierAccountId).HasColumnName("policy_supplier_account_id");
            e.Property(x => x.SupplierEligibilityVersion).HasColumnName("policy_supplier_eligibility_version");
            e.Property(x => x.BudgetVersion).HasColumnName("policy_budget_version");
            e.Property(x => x.BudgetAmountPen).HasColumnName("policy_budget_amount_pen").HasColumnType("numeric(18,4)");
            e.Property(x => x.TotalPen).HasColumnName("policy_total_pen").HasColumnType("numeric(28,8)");
            e.Property(x => x.MarginPen).HasColumnName("policy_margin_pen").HasColumnType("numeric(28,8)");
            e.Property(x => x.ExchangeRate).HasColumnName("policy_exchange_rate").HasColumnType("numeric(18,8)");
            e.Property(x => x.ExchangeRateSource).HasColumnName("policy_exchange_rate_source");
            e.Property(x => x.RateType).HasColumnName("policy_rate_type");
            e.Property(x => x.RatePublishedOn).HasColumnName("policy_rate_published_on");
            e.Property(x => x.RateRetrievedAt).HasColumnName("policy_rate_retrieved_at");
        });
    }
}
