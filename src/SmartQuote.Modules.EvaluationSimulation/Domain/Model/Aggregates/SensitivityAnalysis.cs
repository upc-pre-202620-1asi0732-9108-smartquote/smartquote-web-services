using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;

public record SensitivityWeight(Guid CriterionId, decimal Weight);
public record SensitivityScore(string QuotationId, bool IsEligible, decimal Score, IReadOnlyList<string> Exclusions);
public record SensitivityInputEvidence(Guid BaselineConfigurationId, long TechnicalRevision, DateOnly RequiredDate, string Priority,
    IReadOnlyList<SensitivityItemEvidence> Items, IReadOnlyList<SensitivityQuoteEvidence> Quotations);
public record SensitivityItemEvidence(string ItemId, decimal Quantity, string UnitOfMeasure, IReadOnlyList<RequestRequirementSnapshotData> Requirements);
public record SensitivityQuoteEvidence(string QuotationId, long Version, Guid? SupplierAccountId, long EligibilityVersion, DateOnly? ValidUntil, string Currency,
    int DeliveryLeadTimeDays, IReadOnlyList<SensitivityLineEvidence> Lines);
public record SensitivityLineEvidence(string? LineId, string? ItemId, decimal Quantity, string UnitOfMeasure, decimal UnitPrice, IReadOnlyList<QuotationSpecificationSnapshotData> Specifications);
public record SensitivityConfiguration(Guid Id, IReadOnlyList<SensitivityWeight> Weights,
    IReadOnlyList<SensitivityScore> Results, IReadOnlyList<string> TiedWinnerIds, string? RecommendedQuotationId);

/// <summary>US19: independent research evidence, never a source for purchase-order issuance.</summary>
public sealed class SensitivityAnalysis
{
    public Guid Id { get; private set; }
    public Guid ScenarioId { get; private set; }
    public string RequestId { get; private set; } = "";
    public int CriteriaVersion { get; private set; }
    public string InputFingerprint { get; private set; } = "";
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public SensitivityInputEvidence Inputs { get; private set; } = null!;
    public ExchangeRateSnapshot? ExchangeRate { get; private set; }
    public IReadOnlyList<SensitivityConfiguration> Configurations { get; private set; } = new List<SensitivityConfiguration>();
    private SensitivityAnalysis() { }
    public static SensitivityAnalysis Create(EvaluationScenario scenario, EvaluationDataset dataset, ExchangeRateSnapshot? rate,
        IReadOnlyList<SensitivityConfiguration> configurations, Guid actor, DateTimeOffset now)
    {
        if (configurations.Count < 2 || configurations.Any(c => c.Results.Count(r => r.IsEligible) < 2))
            throw new DomainException("Sensitivity requires at least two eligible quotations in every configuration.");
        return new() { Id = Guid.NewGuid(), ScenarioId = scenario.Id.Value, RequestId = scenario.RequestId, CriteriaVersion = scenario.Version,
            InputFingerprint = ValueObjects.InputFingerprint.FromParts(dataset.CalculateFingerprint(rate, DateOnly.FromDateTime(now.UtcDateTime.AddHours(-5))).Value, scenario.CalculateDefinitionFingerprint().Value).Value,
            CreatedBy = actor, CreatedAt = now,
            Inputs = new(configurations[0].Id, dataset.Request.TechnicalRevision, dataset.Request.RequiredDate, dataset.Request.Priority,
                dataset.Request.Items.Select(i => new SensitivityItemEvidence(i.SourceRequestedItemId, i.Quantity, i.UnitOfMeasure, i.Requirements)).ToList(),
                dataset.Quotations.Select(q => new SensitivityQuoteEvidence(q.QuotationId, q.Version, q.SupplierAccountId, q.SupplierEligibilityVersion, q.ValidUntil, q.Currency,
                    q.DeliveryLeadTimeDays, q.Lines.Select(l => new SensitivityLineEvidence(l.SourceQuotationLineId, l.SourceRequestedItemId, l.Quantity, l.UnitOfMeasure, l.UnitPrice, l.Specifications)).ToList())).ToList()),
            ExchangeRate = rate, Configurations = configurations };
    }
}
