using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;

public static class EvaluationSimulationModelBuilderExtensions
{
    private const string Schema = "evaluation_simulation";

    public static void ApplyEvaluationSimulationConfiguration(this ModelBuilder builder)
    {
        ConfigureEvaluationScenario(builder);
        ConfigureSimulationRun(builder);
    }

    private static void ConfigureEvaluationScenario(ModelBuilder builder)
    {
        builder.Entity<EvaluationScenario>(entity =>
        {
            entity.ToTable("evaluation_scenarios", Schema);

            entity.HasKey(scenario => scenario.Id);
            entity.Property(scenario => scenario.Id)
                .HasConversion(id => id.Value, value => new EvaluationScenarioId(value))
                .ValueGeneratedNever();

            entity.Property(scenario => scenario.RequestId)
                .HasColumnName("purchase_request_id")
                .HasConversion(id => Guid.Parse(id), value => value.ToString())
                .HasColumnType("uuid")
                .IsRequired();

            entity.Property(scenario => scenario.Version).IsRequired();
            entity.Property(scenario => scenario.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            entity.Property(scenario => scenario.CreatedBy)
                .HasConversion(userId => userId.Value, value => new UserId(value))
                .IsRequired();

            entity.Property(scenario => scenario.CreatedAt).IsRequired();

            entity.Property(scenario => scenario.SupersedesScenarioId)
                .HasConversion(id => id == null ? (Guid?)null : id.Value, value => value == null ? null : new EvaluationScenarioId(value.Value));

            entity.HasOne<EvaluationScenario>()
                .WithMany()
                .HasForeignKey(scenario => scenario.SupersedesScenarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.OwnsMany(scenario => scenario.Criteria, criteria =>
            {
                criteria.ToTable("evaluation_criteria", Schema);
                criteria.WithOwner().HasForeignKey("evaluation_scenario_id");
                criteria.HasKey(criterion => criterion.Id);

                criteria.Property(criterion => criterion.Id)
                    .HasConversion(id => id.Value, value => new Domain.Model.ValueObjects.EvaluationCriterionId(value))
                    .ValueGeneratedNever();

                criteria.Property(criterion => criterion.Name).IsRequired().HasMaxLength(150);
                criteria.Property(criterion => criterion.TargetField).HasColumnName("target_field").IsRequired().HasMaxLength(150);
                criteria.Property(criterion => criterion.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
                criteria.Property(criterion => criterion.Mode).HasConversion<string>().HasMaxLength(20).IsRequired();
                criteria.Property(criterion => criterion.Operator).HasColumnName("comparison_operator").HasConversion<string>().HasMaxLength(30).IsRequired();
                criteria.Property(criterion => criterion.ExpectedValue).IsRequired().HasMaxLength(250);
                criteria.Property(criterion => criterion.UnitOfMeasure).HasMaxLength(30);
                criteria.Property(criterion => criterion.Weight).HasColumnType("numeric(5,2)").IsRequired();
                criteria.Property(criterion => criterion.DisplayOrder).HasColumnName("display_order").IsRequired();
            });

            entity.Navigation(scenario => scenario.Criteria).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasIndex(scenario => new { scenario.RequestId, scenario.Version }).IsUnique();
        });
    }

    private static void ConfigureSimulationRun(ModelBuilder builder)
    {
        builder.Entity<SimulationRun>(entity =>
        {
            entity.ToTable("simulation_runs", Schema);

            entity.HasKey(run => run.Id);
            entity.Property(run => run.Id)
                .HasConversion(id => id.Value, value => new SimulationRunId(value))
                .ValueGeneratedNever();

            entity.Property(run => run.ScenarioId)
                .HasColumnName("evaluation_scenario_id")
                .HasConversion(id => id.Value, value => new EvaluationScenarioId(value))
                .IsRequired();

            entity.HasOne<EvaluationScenario>()
                .WithMany()
                .HasForeignKey(run => run.ScenarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(run => run.CriteriaVersion).HasColumnName("criteria_version").IsRequired();

            entity.Property(run => run.InputFingerprint)
                .HasColumnName("input_fingerprint")
                .HasConversion(fp => fp.Value, value => new InputFingerprint(value))
                .HasColumnType("char(64)")
                .IsRequired();

            entity.Property(run => run.ExecutedAt).IsRequired();

            ConfigureRequestSnapshot(entity);
            ConfigureQuotationSnapshots(entity);
            ConfigureEvaluations(entity);
            ConfigureRecommendation(entity);
        });
    }

    private static void ConfigureRequestSnapshot(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SimulationRun> entity)
    {
        entity.OwnsOne(run => run.RequestSnapshot, snapshot =>
        {
            snapshot.ToTable("simulation_request_snapshots", Schema);
            snapshot.WithOwner().HasForeignKey("simulation_run_id");

            snapshot.Property(s => s.RequestId)
                .HasColumnName("source_request_id")
                .HasConversion(id => Guid.Parse(id), value => value.ToString())
                .HasColumnType("uuid")
                .IsRequired();

            snapshot.Property(s => s.Version).HasColumnName("source_request_version").IsRequired();
            snapshot.Property(s => s.RequiredDate).IsRequired();
            snapshot.Property(s => s.Priority).HasMaxLength(20).IsRequired();
            snapshot.Property(s => s.CapturedAt).IsRequired();

            snapshot.OwnsMany(s => s.Items, items =>
            {
                items.ToTable("simulation_request_items", Schema);
                items.WithOwner().HasForeignKey("simulation_request_id");
                items.Property<SimulationRunId>("simulation_request_id").IsRequired();
                items.Property<Guid>("id").ValueGeneratedOnAdd();
                items.HasKey("id");

                items.Property(item => item.SourceRequestedItemId)
                    .HasColumnName("source_requested_item_id")
                    .HasConversion(id => Guid.Parse(id), value => value.ToString())
                    .HasColumnType("uuid")
                    .IsRequired();

                items.Property(item => item.LineNumber).IsRequired();
                items.Property(item => item.Description).IsRequired().HasMaxLength(250);
                items.Property(item => item.Quantity).HasColumnType("numeric(18,4)").IsRequired();
                items.Property(item => item.UnitOfMeasure).IsRequired().HasMaxLength(30);

                items.OwnsMany(item => item.Requirements, requirements =>
                {
                    requirements.ToTable("simulation_request_requirements", Schema);
                    requirements.WithOwner().HasForeignKey("simulation_request_item_id");
                    requirements.Property<Guid>("id").ValueGeneratedOnAdd();
                    requirements.HasKey("id");

                    requirements.Property(req => req.SourceRequirementId)
                        .HasColumnName("source_requirement_id")
                        .HasConversion(id => id == null ? (Guid?)null : Guid.Parse(id), value => value == null ? null : value.ToString())
                        .HasColumnType("uuid");

                    requirements.Property(req => req.Name).IsRequired().HasMaxLength(150);
                    requirements.Property(req => req.Operator).HasColumnName("comparison_operator").HasConversion<string>().HasMaxLength(30).IsRequired();
                    requirements.Property(req => req.ExpectedValue).IsRequired().HasMaxLength(250);
                    requirements.Property(req => req.UnitOfMeasure).HasMaxLength(30);
                    requirements.Property(req => req.IsMandatory).IsRequired();
                });

                items.Navigation(item => item.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            snapshot.Navigation(s => s.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private static void ConfigureQuotationSnapshots(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SimulationRun> entity)
    {
        entity.OwnsMany(run => run.QuotationSnapshots, snapshots =>
        {
            snapshots.ToTable("simulation_quotation_snapshots", Schema);
            snapshots.WithOwner().HasForeignKey("simulation_run_id");
            snapshots.Property<SimulationRunId>("simulation_run_id").IsRequired();

            snapshots.Property(s => s.QuotationId)
                .HasColumnName("source_quotation_id")
                .HasConversion(id => Guid.Parse(id), value => value.ToString())
                .HasColumnType("uuid")
                .IsRequired();

            snapshots.HasKey("simulation_run_id", nameof(QuotationEvaluationSnapshot.QuotationId));

            snapshots.Property(s => s.Version).HasColumnName("source_quotation_version").IsRequired();
            snapshots.Property(s => s.SupplierId).HasColumnName("supplier_reference_id").IsRequired().HasMaxLength(100);
            snapshots.Property(s => s.SupplierBusinessName).IsRequired().HasMaxLength(200);
            snapshots.Property(s => s.SupplierTaxIdentifier).IsRequired().HasMaxLength(20);
            snapshots.Property(s => s.Currency).HasColumnType("char(3)").IsRequired();
            snapshots.Property(s => s.DeliveryLeadTimeDays).IsRequired();
            snapshots.Property(s => s.VerifiedAt).IsRequired();
            snapshots.Property(s => s.CapturedAt).IsRequired();

            snapshots.OwnsMany(s => s.Lines, lines =>
            {
                lines.ToTable("simulation_quotation_lines", Schema);
                lines.WithOwner().HasForeignKey("simulation_run_id", "source_quotation_id");
                lines.Property<SimulationRunId>("simulation_run_id").IsRequired();
                lines.Property<string>("source_quotation_id")
                    .HasConversion(id => Guid.Parse(id), value => value.ToString())
                    .HasColumnType("uuid")
                    .IsRequired();
                lines.Property<Guid>("id").ValueGeneratedOnAdd();
                lines.HasKey("id");

                lines.Property(line => line.SourceQuotationLineId)
                    .HasColumnName("source_quotation_line_id")
                    .HasConversion(id => id == null ? (Guid?)null : Guid.Parse(id), value => value == null ? null : value.ToString())
                    .HasColumnType("uuid");

                lines.Property(line => line.SourceRequestedItemId)
                    .HasColumnName("source_requested_item_id")
                    .HasConversion(id => id == null ? (Guid?)null : Guid.Parse(id), value => value == null ? null : value.ToString())
                    .HasColumnType("uuid");

                lines.Property(line => line.LineNumber).IsRequired();
                lines.Property(line => line.Description).IsRequired().HasMaxLength(250);
                lines.Property(line => line.Quantity).HasColumnType("numeric(18,4)").IsRequired();
                lines.Property(line => line.UnitOfMeasure).IsRequired().HasMaxLength(30);
                lines.Property(line => line.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();

                lines.OwnsMany(line => line.Specifications, specs =>
                {
                    specs.ToTable("simulation_quotation_specifications", Schema);
                    specs.WithOwner().HasForeignKey("simulation_quotation_line_id");
                    specs.Property<Guid>("id").ValueGeneratedOnAdd();
                    specs.HasKey("id");

                    specs.Property(spec => spec.Name).IsRequired().HasMaxLength(150);
                    specs.Property(spec => spec.Value).IsRequired().HasMaxLength(250);
                    specs.Property(spec => spec.UnitOfMeasure).HasMaxLength(30);
                });

                lines.Navigation(line => line.Specifications).UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            snapshots.Navigation(s => s.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        entity.Navigation(run => run.QuotationSnapshots).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureEvaluations(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SimulationRun> entity)
    {
        entity.OwnsMany(run => run.Evaluations, evaluations =>
        {
            evaluations.ToTable("quotation_evaluations", Schema);
            evaluations.WithOwner().HasForeignKey("simulation_run_id");
            evaluations.Property<Guid>("id").ValueGeneratedOnAdd();
            evaluations.HasKey("id");

            evaluations.Property(evaluation => evaluation.QuotationId)
                .HasColumnName("quotation_id")
                .HasConversion(id => Guid.Parse(id), value => value.ToString())
                .HasColumnType("uuid")
                .IsRequired();

            evaluations.Property(evaluation => evaluation.IsEligible).HasColumnName("is_eligible").IsRequired();

            evaluations.OwnsOne(evaluation => evaluation.TotalScore, score =>
            {
                score.WithOwner().HasForeignKey("id");
                score.Property(s => s.Value).HasColumnName("total_score").HasColumnType("numeric(7,4)").IsRequired();
            });

            evaluations.Property(evaluation => evaluation.Rank).HasColumnName("ranking_position");

            evaluations.OwnsMany(evaluation => evaluation.CriterionResults, results =>
            {
                results.ToTable("criterion_results", Schema);
                results.WithOwner().HasForeignKey("quotation_evaluation_id");
                results.Property<Guid>("id").ValueGeneratedOnAdd();
                results.HasKey("id");

                results.Property(result => result.CriterionId)
                    .HasColumnName("evaluation_criterion_id")
                    .HasConversion(id => id.Value, value => new Domain.Model.ValueObjects.EvaluationCriterionId(value))
                    .HasColumnType("uuid")
                    .IsRequired();

                results.Property(result => result.Passed).IsRequired();
                results.Property(result => result.NormalizedScore).HasColumnName("normalized_score").HasColumnType("numeric(7,4)").IsRequired();
                results.Property(result => result.WeightedContribution).HasColumnName("weighted_contribution").HasColumnType("numeric(7,4)").IsRequired();
                results.Property(result => result.Explanation).IsRequired();
            });

            evaluations.OwnsMany(evaluation => evaluation.ExclusionReasons, reasons =>
            {
                reasons.ToTable("exclusion_reasons", Schema);
                reasons.WithOwner().HasForeignKey("quotation_evaluation_id");
                reasons.Property<Guid>("id").ValueGeneratedOnAdd();
                reasons.HasKey("id");

                reasons.Property(reason => reason.CriterionId)
                    .HasColumnName("evaluation_criterion_id")
                    .HasConversion(id => id.Value, value => new Domain.Model.ValueObjects.EvaluationCriterionId(value))
                    .HasColumnType("uuid")
                    .IsRequired();

                reasons.Property(reason => reason.Code).IsRequired().HasMaxLength(50);
                reasons.Property(reason => reason.Explanation).IsRequired();
            });

            evaluations.Navigation(evaluation => evaluation.CriterionResults).UsePropertyAccessMode(PropertyAccessMode.Field);
            evaluations.Navigation(evaluation => evaluation.ExclusionReasons).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        entity.Navigation(run => run.Evaluations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureRecommendation(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SimulationRun> entity)
    {
        entity.OwnsOne(run => run.Recommendation, recommendation =>
        {
            recommendation.ToTable("simulation_recommendations", Schema);
            recommendation.WithOwner().HasForeignKey("simulation_run_id");

            recommendation.Property(r => r.QuotationId)
                .HasColumnName("quotation_id")
                .HasConversion(id => Guid.Parse(id), value => value.ToString())
                .HasColumnType("uuid")
                .IsRequired();

            recommendation.OwnsOne(r => r.Score, score =>
            {
                score.WithOwner().HasForeignKey("simulation_run_id");
                score.Property(s => s.Value).HasColumnName("score").HasColumnType("numeric(7,4)").IsRequired();
            });

            recommendation.Property(r => r.Explanation).IsRequired();
        });
    }
}
