using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

public static class DeliveryEvaluationModelBuilderExtensions
{
    private const string Schema = "purchase_ordering";

    public static void ApplyDeliveryEvaluationConfiguration(this ModelBuilder builder)
    {
        builder.Entity<DeliveryEvaluation>(entity =>
        {
            entity.ToTable("delivery_evaluations", Schema);

            entity.HasKey(evaluation => evaluation.Id);
            entity.Property(evaluation => evaluation.Id)
                .HasConversion(id => id.Value, value => new DeliveryEvaluationId(value))
                .ValueGeneratedNever();

            entity.Property(evaluation => evaluation.PurchaseOrderId)
                .HasConversion(id => id.Value, value => new PurchaseOrderId(value))
                .IsRequired();
            entity.HasIndex(evaluation => evaluation.PurchaseOrderId).IsUnique();

            entity.Property(evaluation => evaluation.SupplierTaxIdentifier).HasMaxLength(20).IsRequired();
            entity.HasIndex(evaluation => evaluation.SupplierTaxIdentifier);

            entity.Property(evaluation => evaluation.OnTimeScore).IsRequired();
            entity.Property(evaluation => evaluation.QualityScore).IsRequired();
            entity.Property(evaluation => evaluation.Observations).HasMaxLength(500);

            entity.Property(evaluation => evaluation.EvaluatedBy)
                .HasConversion(userId => userId.Value, value => new UserId(value))
                .IsRequired();
            entity.Property(evaluation => evaluation.EvaluatedAt).IsRequired();
        });
    }
}
