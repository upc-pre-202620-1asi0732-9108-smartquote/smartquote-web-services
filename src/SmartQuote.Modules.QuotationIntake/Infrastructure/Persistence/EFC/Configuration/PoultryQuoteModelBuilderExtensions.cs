using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;
using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Configuration;

public static class PoultryQuoteModelBuilderExtensions
{
    private const string Schema = "quotation_intake";

    public static void ApplyQuotationIntakeConfiguration(this ModelBuilder builder)
    {
        builder.Entity<PoultryQuote>(entity =>
        {
            entity.ToTable("poultry_quotes", Schema);

            entity.HasKey(quote => quote.Id);
            entity.Property(quote => quote.Id)
                .HasConversion(id => id.Value, value => new PoultryQuoteId(value))
                .ValueGeneratedNever();

            entity.Property(quote => quote.RequestReference)
                .HasColumnName("purchase_request_id")
                .HasConversion(reference => Guid.Parse(reference.RequestId), value => new PurchaseRequestReference(value.ToString()))
                .HasColumnType("uuid")
                .IsRequired();

            entity.OwnsOne(quote => quote.Supplier, supplier =>
            {
                supplier.WithOwner().HasForeignKey("Id");
                supplier.Property(s => s.SupplierId).HasColumnName("supplier_reference_id").IsRequired().HasMaxLength(100);
                supplier.Property(s => s.BusinessName).HasColumnName("supplier_business_name").IsRequired().HasMaxLength(200);
                supplier.Property(s => s.TaxIdentifier).HasColumnName("supplier_tax_identifier").IsRequired().HasMaxLength(20);
            });

            entity.OwnsOne(quote => quote.SourceDocument, document =>
            {
                document.WithOwner().HasForeignKey("Id");
                document.Property(d => d.FileName).HasColumnName("source_file_name").IsRequired().HasMaxLength(255);
                document.Property(d => d.ContentType).HasColumnName("source_content_type").IsRequired().HasMaxLength(100);
                document.Property(d => d.StorageKey).HasColumnName("source_storage_key").IsRequired().HasMaxLength(500);
                document.Property(d => d.Sha256Hash).HasColumnName("source_sha256").IsRequired().HasColumnType("char(64)");
            });

            entity.Property(quote => quote.DuplicateKey)
                .HasColumnName("duplicate_key")
                .HasMaxLength(110)
                .IsRequired();
            entity.HasIndex(quote => quote.DuplicateKey).IsUnique();

            entity.Property(quote => quote.ValidUntil);
            entity.Property(quote => quote.Currency).HasColumnType("char(3)");
            entity.Property(quote => quote.DeliveryLeadTimeDays);

            entity.Property(quote => quote.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(quote => quote.Version).IsRequired().IsConcurrencyToken();

            entity.Property(quote => quote.VerifiedBy)
                .HasConversion(userId => userId == null ? (Guid?)null : userId.Value, value => value == null ? null : new UserId(value.Value));

            entity.Property(quote => quote.VerifiedAt);
            entity.Property(quote => quote.RejectionReason);

            entity.Property(quote => quote.CreatedAt).IsRequired();
            entity.Property(quote => quote.UpdatedAt).IsRequired();

            entity.OwnsMany(quote => quote.Lines, linesBuilder =>
            {
                linesBuilder.ToTable("quotation_lines", Schema);
                linesBuilder.WithOwner().HasForeignKey("poultry_quote_id");
                linesBuilder.HasKey(line => line.Id);

                linesBuilder.Property(line => line.Id)
                    .HasConversion(id => id.Value, value => new QuotationLineId(value))
                    .ValueGeneratedNever();

                linesBuilder.Property(line => line.RequestedItemId)
                    .HasColumnName("requested_item_id")
                    .HasConversion(id => id == null ? (Guid?)null : Guid.Parse(id), value => value == null ? null : value.ToString())
                    .HasColumnType("uuid");

                linesBuilder.Property(line => line.LineNumber).IsRequired();
                linesBuilder.Property(line => line.Description).HasMaxLength(250);
                linesBuilder.Property(line => line.Quantity).HasColumnType("numeric(18,4)");
                linesBuilder.Property(line => line.UnitOfMeasure).HasMaxLength(30);
                linesBuilder.Property(line => line.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(18,4)");

                linesBuilder.OwnsMany(line => line.Specifications, specificationsBuilder =>
                {
                    specificationsBuilder.ToTable("quoted_specifications", Schema);
                    specificationsBuilder.WithOwner().HasForeignKey("quotation_line_id");
                    specificationsBuilder.Property("quotation_line_id").IsRequired();
                    specificationsBuilder.Property<Guid>("id").ValueGeneratedOnAdd();
                    specificationsBuilder.HasKey("id");

                    specificationsBuilder.Property(spec => spec.Name).IsRequired().HasMaxLength(150);
                    specificationsBuilder.Property(spec => spec.Value).IsRequired().HasMaxLength(250);
                    specificationsBuilder.Property(spec => spec.UnitOfMeasure).HasMaxLength(30);
                });

                linesBuilder.Navigation(line => line.Specifications).UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            entity.OwnsMany(quote => quote.Fields, fieldsBuilder =>
            {
                fieldsBuilder.ToTable("extracted_fields", Schema);
                fieldsBuilder.WithOwner().HasForeignKey("poultry_quote_id");
                fieldsBuilder.HasKey(field => field.Id);

                fieldsBuilder.Property(field => field.Id)
                    .HasConversion(id => id.Value, value => new ExtractedFieldId(value))
                    .ValueGeneratedNever();

                fieldsBuilder.Property(field => field.FieldPath).HasColumnName("field_path").IsRequired().HasMaxLength(250);
                fieldsBuilder.Property(field => field.OriginalValue).HasColumnName("original_value");
                fieldsBuilder.Property(field => field.CurrentValue).HasColumnName("current_value");
                fieldsBuilder.Property(field => field.IsRequired).HasColumnName("is_required").IsRequired();

                fieldsBuilder.OwnsOne(field => field.Confidence, confidence =>
                {
                    confidence.WithOwner().HasForeignKey("Id");
                    confidence.Property(c => c.Value).HasColumnName("confidence").HasColumnType("numeric(5,4)").IsRequired();
                });

                fieldsBuilder.OwnsOne(field => field.Source, source =>
                {
                    source.WithOwner().HasForeignKey("Id");
                    source.Property(s => s.PageNumber).HasColumnName("source_page").IsRequired();
                    source.Property(s => s.TextReference).HasColumnName("source_text_reference");
                });

                fieldsBuilder.Property(field => field.Status)
                    .HasColumnName("resolution_status")
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .IsRequired();

                fieldsBuilder.OwnsMany(field => field.Corrections, correctionsBuilder =>
                {
                    correctionsBuilder.ToTable("field_corrections", Schema);
                    correctionsBuilder.WithOwner().HasForeignKey("extracted_field_id");
                    correctionsBuilder.Property("extracted_field_id").IsRequired();
                    correctionsBuilder.Property<Guid>("id").ValueGeneratedOnAdd();
                    correctionsBuilder.HasKey("id");

                    correctionsBuilder.Property(c => c.PreviousValue).HasColumnName("previous_value").IsRequired();
                    correctionsBuilder.Property(c => c.CorrectedValue).HasColumnName("corrected_value").IsRequired();

                    correctionsBuilder.Property(c => c.CorrectedBy)
                        .HasColumnName("corrected_by")
                        .HasConversion(userId => userId.Value, value => new UserId(value))
                        .IsRequired();

                    correctionsBuilder.Property(c => c.CorrectedAt).HasColumnName("corrected_at").IsRequired();
                    correctionsBuilder.Property(c => c.Reason).HasColumnName("reason").IsRequired();
                });

                fieldsBuilder.Navigation(field => field.Corrections).UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            entity.Navigation(quote => quote.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(quote => quote.Fields).UsePropertyAccessMode(PropertyAccessMode.Field);

        });
    }
}
