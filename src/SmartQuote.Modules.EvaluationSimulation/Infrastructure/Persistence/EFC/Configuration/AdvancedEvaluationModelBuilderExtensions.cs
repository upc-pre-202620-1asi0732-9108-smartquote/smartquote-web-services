using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;

public static class AdvancedEvaluationModelBuilderExtensions
{
    public static void ApplyAdvancedEvaluationConfiguration(this ModelBuilder b)
    {
        b.Entity<SimulationRun>().Property(r => r.Mode).HasMaxLength(30).HasDefaultValue("SingleSupplier");
        Json(b.Entity<SimulationRun>().Property(r => r.Distribution));
        b.Entity<SensitivityAnalysis>(e => {
            e.ToTable("sensitivity_analyses", "evaluation_simulation"); e.HasKey(a => a.Id);
            e.Property(a => a.InputFingerprint).HasMaxLength(64);
            Json(e.Property(a => a.Inputs)); Json(e.Property(a => a.ExchangeRate));
            e.OwnsMany(a => a.Configurations, c => {
                c.ToTable("sensitivity_configurations", "evaluation_simulation"); c.WithOwner().HasForeignKey("analysis_id"); c.HasKey(v => v.Id);
                c.Property(v => v.Id).ValueGeneratedNever();
                Json(c.Property(v => v.Weights)); Json(c.Property(v => v.Results)); Json(c.Property(v => v.TiedWinnerIds));
            });
            e.Navigation(a => a.Configurations).AutoInclude();
            e.HasIndex(a => new { a.RequestId, a.CreatedAt });
        });
    }
    public static void Json<T>(PropertyBuilder<T> p)
    {
        p.HasColumnType("jsonb").HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), v => JsonSerializer.Deserialize<T>(v, (JsonSerializerOptions?)null)!);
        p.Metadata.SetValueComparer(new ValueComparer<T>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            a => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null).GetHashCode(),
            a => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(a, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
    }
}
