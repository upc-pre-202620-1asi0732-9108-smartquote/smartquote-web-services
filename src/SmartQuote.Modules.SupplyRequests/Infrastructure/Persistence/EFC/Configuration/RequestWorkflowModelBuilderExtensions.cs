using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;

public static class RequestWorkflowModelBuilderExtensions
{
    private static string Encode(IReadOnlyList<ScheduleItem> value) => JsonSerializer.Serialize(value);
    private static IReadOnlyList<ScheduleItem> Decode(string value) => JsonSerializer.Deserialize<List<ScheduleItem>>(value)!;
    public static void ApplyRequestWorkflowConfiguration(this ModelBuilder builder)
    {
        builder.Entity<PurchaseRequest>(e =>
        {
            e.Ignore(r => r.IsActive);
            e.HasIndex(r => new { r.AssignedAnalystId, r.Status });
            e.HasIndex(r => r.ScheduleOccurrenceId).IsUnique();
            e.OwnsMany(r => r.AssignmentHistory, h =>
            {
                h.ToTable("request_assignment_history", "supply_requests");
                h.WithOwner().HasForeignKey("purchase_request_id"); h.HasKey(x => x.Id);
                h.Property(x => x.Id).ValueGeneratedNever(); h.Property(x => x.Reason).HasMaxLength(2000);
            });
            e.OwnsMany(r => r.RequirementRevisions, h =>
            {
                h.ToTable("requirement_revisions", "supply_requests");
                h.WithOwner().HasForeignKey("purchase_request_id"); h.HasKey(x => x.Id);
                h.Property(x => x.Id).ValueGeneratedNever(); h.Property(x => x.Reason).HasMaxLength(2000);
                h.HasIndex("purchase_request_id", "TechnicalRevision").IsUnique();
                h.OwnsOne(x => x.Previous, p =>
                {
                    p.WithOwner().HasForeignKey("Id");
                    p.Property(x => x.Name).HasColumnName("previous_name");
                    p.Property(x => x.Operator).HasColumnName("previous_operator");
                    p.Property(x => x.ExpectedValue).HasColumnName("previous_expected_value");
                    p.Property(x => x.UnitOfMeasure).HasColumnName("previous_unit_of_measure");
                    p.Property(x => x.IsMandatory).HasColumnName("previous_is_mandatory");
                });
                h.OwnsOne(x => x.Current, p =>
                {
                    p.WithOwner().HasForeignKey("Id");
                    p.Property(x => x.Name).HasColumnName("current_name");
                    p.Property(x => x.Operator).HasColumnName("current_operator");
                    p.Property(x => x.ExpectedValue).HasColumnName("current_expected_value");
                    p.Property(x => x.UnitOfMeasure).HasColumnName("current_unit_of_measure");
                    p.Property(x => x.IsMandatory).HasColumnName("current_is_mandatory");
                });
            });
            e.Navigation(r => r.AssignmentHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(r => r.RequirementRevisions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<TechnicalClarification>(e =>
        {
            e.ToTable("technical_clarifications", "supply_requests"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever(); e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.RequestId).HasConversion(id => id.Value, value => new SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects.PurchaseRequestId(value));
            e.Property(x => x.Question).HasMaxLength(4000); e.Property(x => x.Answer).HasMaxLength(4000);
            e.Property(x => x.Status).HasMaxLength(30);
            e.HasIndex(x => new { x.SpecialistId, x.Status }); e.HasIndex(x => x.RequestId);
            e.HasOne<PurchaseRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RecurringRequestSchedule>(e =>
        {
            e.ToTable("recurring_request_schedules", "supply_requests"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever(); e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.TimeZoneId).HasMaxLength(100); e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Items).HasColumnType("jsonb").HasConversion(v => Encode(v), v => Decode(v))
                .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<ScheduleItem>>(
                    (a, b) => Encode(a!) == Encode(b!), v => Encode(v).GetHashCode(), v => Decode(Encode(v))));
            e.HasIndex(x => new { x.IsActive, x.NextOccurrenceAt }); e.HasIndex(x => x.OwnerId);
            e.OwnsMany(x => x.Occurrences, o =>
            {
                o.ToTable("schedule_occurrences", "supply_requests"); o.WithOwner().HasForeignKey("schedule_id");
                o.HasKey(x => x.Id); o.Property(x => x.Id).ValueGeneratedNever();
                o.HasIndex("schedule_id", "ScheduledDate").IsUnique(); o.HasIndex(x => x.RequestId).IsUnique();
                o.Property(x => x.Status).HasMaxLength(30);
            });
            e.Navigation(x => x.Occurrences).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
