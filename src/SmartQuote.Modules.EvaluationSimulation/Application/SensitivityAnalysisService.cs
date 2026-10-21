using Microsoft.Extensions.Configuration;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Services;

namespace SmartQuote.Modules.EvaluationSimulation.Application;

public record SensitivityAnalysisView(SensitivityAnalysis Analysis, bool IsCurrent, bool IsStable, string Scope);
public sealed class SensitivityAnalysisService(EvaluationPreparationService preparation, SimulationEngine engine,
    IAdvancedEvaluationStore store, IEvaluationScenarioRepository scenarios, ISimulationRunRepository runs,
    ICurrentUser user, TimeProvider clock, IConfiguration configuration)
{
    public async Task<SensitivityAnalysisView> CreateAsync(Guid scenarioId, IReadOnlyList<IReadOnlyList<SensitivityWeight>> alternatives, CancellationToken ct)
    {
        RequireManager();
        var maximum = Math.Clamp(configuration.GetValue("Sensitivity:MaxConfigurations", 20), 1, 100);
        if (alternatives.Count is < 1 || alternatives.Count > maximum) throw new ArgumentException($"Provide between 1 and {maximum} configurations.");
        var p = await preparation.PrepareAsync(scenarioId, false, null, ct);
        var baseWeights = p.Scenario.Criteria.Where(c => c.Mode == CriterionMode.Weighted).Select(c => new SensitivityWeight(c.Id.Value, c.Weight)).ToList();
        // Validate the whole batch before evaluating or persisting anything.
        var configurations = new List<IReadOnlyList<SensitivityWeight>> { baseWeights };
        configurations.AddRange(alternatives);
        foreach (var weights in configurations) Validate(p.Scenario, weights);
        var results = configurations.Select(weights => Evaluate(p, weights)).ToList();
        var analysis = SensitivityAnalysis.Create(p.Scenario, p.Dataset, p.Rate, results, user.UserId, clock.GetUtcNow());
        store.Add(analysis); await store.SaveAsync(ct);
        return View(analysis, true);
    }
    public async Task<SensitivityAnalysisView> GetAsync(Guid id, CancellationToken ct)
    {
        RequireManager();
        var analysis = await store.FindAsync(id, ct) ?? throw new NotFoundException("Sensitivity analysis was not found.");
        bool current;
        try { var p = await preparation.PrepareAsync(analysis.ScenarioId, true, analysis.ExchangeRate, ct); current = IsCurrent(analysis, p); }
        catch (DomainException) { current = false; }
        catch (ConflictException) { current = false; }
        return View(analysis, current);
    }
    public async Task<IReadOnlyList<SensitivityAnalysisView>> ListAsync(Guid requestId, CancellationToken ct)
    {
        var result = new List<SensitivityAnalysisView>();
        foreach (var a in await store.ListAsync(requestId.ToString(), ct)) result.Add(await GetAsync(a.Id, ct));
        return result;
    }
    public async Task<object> PromoteAsync(Guid analysisId, Guid configurationId, int expectedCriteriaVersion, CancellationToken ct)
    {
        RequireManager();
        await using var lease = await store.LockAsync(ct);
        var analysis = await store.FindAsync(analysisId, ct) ?? throw new NotFoundException("Sensitivity analysis was not found.");
        var variant = analysis.Configurations.SingleOrDefault(c => c.Id == configurationId) ?? throw new NotFoundException("Sensitivity configuration was not found.");
        var p = await preparation.PrepareAsync(analysis.ScenarioId, true, analysis.ExchangeRate, ct);
        if (p.Scenario.Version != expectedCriteriaVersion || !IsCurrent(analysis, p)) throw new ConflictException("Analysis inputs changed; repeat the analysis before promotion.");
        var next = Copy(p.Scenario, variant.Weights, true, user.UserId);
        var run = engine.Run(next, p.Dataset, p.Rate, p.Today);
        if (run.Evaluations.Count(e => e.IsEligible) < 2) throw new DomainException("At least two eligible quotations are required for promotion.");
        // No supersession until all calculation and validity checks have succeeded. One EF transaction saves both.
        p.Scenario.MarkSuperseded(); await scenarios.AddAsync(next, ct); await runs.AddAsync(run, ct);
        await lease.CommitAsync(ct);
        return new { ScenarioId = next.Id.Value, SimulationRunId = run.Id.Value };
    }
    private SensitivityConfiguration Evaluate(PreparedEvaluation p, IReadOnlyList<SensitivityWeight> weights)
    {
        var scenario = Copy(p.Scenario, weights, false, user.UserId);
        var results = engine.EvaluateCandidates(scenario.Criteria, p.Dataset, p.Rate, p.Today);
        var ranked = results.Where(e => e.IsEligible).OrderByDescending(e => e.TotalScore.Value).ThenBy(e => e.QuotationId, StringComparer.Ordinal).ToList();
        var tied = ranked.Count == 0 ? [] : ranked.Where(e => e.TotalScore.Value == ranked[0].TotalScore.Value).Select(e => e.QuotationId).ToList();
        return new(Guid.NewGuid(), weights.ToList(), results.Select(e => new SensitivityScore(e.QuotationId, e.IsEligible, e.TotalScore.Value,
            e.ExclusionReasons.Select(r => r.Explanation).ToList())).ToList(), tied, ranked.FirstOrDefault()?.QuotationId);
    }
    private static void Validate(EvaluationScenario scenario, IReadOnlyList<SensitivityWeight> weights)
    {
        var editable = scenario.Criteria.Where(c => c.Mode == CriterionMode.Weighted).ToList();
        if (weights.Count != editable.Count || weights.Select(w => w.CriterionId).Distinct().Count() != weights.Count ||
            weights.Any(w => w.Weight is < 0 or > 100 || editable.All(c => c.Id.Value != w.CriterionId)) || weights.Sum(w => w.Weight) != 100)
            throw new DomainException("Weights must cover each weighted criterion exactly once, stay between 0 and 100, and total 100. Mandatory rules cannot be changed.");
        if (editable.Any(c => c.Category == CriterionCategory.TechnicalCompliance && weights.Single(w => w.CriterionId == c.Id.Value).Weight != c.Weight))
            throw new DomainException("Sensitivity changes commercial weights only, not technical constraints.");
    }
    private static EvaluationScenario Copy(EvaluationScenario source, IReadOnlyList<SensitivityWeight> weights, bool nextVersion, Guid actor)
    {
        var copy = nextVersion ? source.CreateNextVersion(new UserId(actor)) : EvaluationScenario.Create(source.RequestId, new UserId(actor));
        copy.BindTechnicalRevision(source.TechnicalRevision);
        foreach (var c in source.Criteria)
            copy.AddCriterion(EvaluationCriterion.Create(c.Name, c.TargetField, c.Category, c.Mode, c.Operator, c.ExpectedValue, c.UnitOfMeasure,
                c.Mode == CriterionMode.Weighted ? weights.Single(w => w.CriterionId == c.Id.Value).Weight : c.Weight, c.DisplayOrder));
        copy.Activate(); return copy;
    }
    private static bool IsCurrent(SensitivityAnalysis a, PreparedEvaluation p) =>
        a.InputFingerprint == InputFingerprint.FromParts(p.Dataset.CalculateFingerprint(p.Rate, p.Today).Value, p.Scenario.CalculateDefinitionFingerprint().Value).Value;
    private static SensitivityAnalysisView View(SensitivityAnalysis a, bool current) => new(a, current,
        a.Configurations.Select(c => c.RecommendedQuotationId).Distinct().Count() == 1,
        "Conclusiones limitadas a las configuraciones evaluadas; los empates usan el identificador de cotización como desempate determinista.");
    private void RequireManager() { if (!user.IsInRole(SmartQuoteRoles.PurchaseManager)) throw new UnauthorizedAccessException(); }
}
