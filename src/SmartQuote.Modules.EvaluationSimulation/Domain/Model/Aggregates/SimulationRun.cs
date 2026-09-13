using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Events;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;

public class SimulationRun : AggregateRoot<SimulationRunId>
{
    private readonly List<QuotationEvaluation> _evaluations = [];
    private readonly List<QuotationEvaluationSnapshot> _quotationSnapshots = [];

    public EvaluationScenarioId ScenarioId { get; private set; } = null!;
    public int CriteriaVersion { get; private set; }
    public InputFingerprint InputFingerprint { get; private set; } = null!;
    public DateTimeOffset ExecutedAt { get; private set; }
    public Recommendation? Recommendation { get; private set; }

    /// <summary>
    /// Immutable copies of the request and quotations used (docs/database-schema-documentation.md
    /// simulation_request_snapshots / simulation_quotation_snapshots). Not shown on the class diagram's
    /// SimulationRun attribute list, but required for the "snapshots are read-only after capture" rule.
    /// </summary>
    public RequestEvaluationSnapshot RequestSnapshot { get; private set; } = null!;
    public IReadOnlyList<QuotationEvaluationSnapshot> QuotationSnapshots => _quotationSnapshots.AsReadOnly();

    public IReadOnlyList<QuotationEvaluation> Evaluations => _evaluations.AsReadOnly();

    private SimulationRun() { }

    public static SimulationRun Create(
        EvaluationScenarioId scenarioId,
        int criteriaVersion,
        InputFingerprint inputFingerprint,
        RequestEvaluationSnapshot requestSnapshot,
        IReadOnlyList<QuotationEvaluationSnapshot> quotationSnapshots)
    {
        if (quotationSnapshots.Count < 2)
            throw new DomainException("At least two eligible quotations are required to run a simulation.");

        var run = new SimulationRun
        {
            Id = new SimulationRunId(Guid.NewGuid()),
            ScenarioId = scenarioId,
            CriteriaVersion = criteriaVersion,
            InputFingerprint = inputFingerprint,
            ExecutedAt = DateTimeOffset.UtcNow,
            RequestSnapshot = requestSnapshot
        };

        run._quotationSnapshots.AddRange(quotationSnapshots);
        return run;
    }

    public void AddEvaluation(QuotationEvaluation evaluation) => _evaluations.Add(evaluation);

    public void DefineRecommendation()
    {
        var ranked = _evaluations
            .Where(evaluation => evaluation.IsEligible)
            .OrderByDescending(evaluation => evaluation.TotalScore.Value)
            .ThenBy(evaluation => evaluation.QuotationId, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
            ranked[i].AssignRank(i + 1);

        var winner = ranked.FirstOrDefault();

        Recommendation = winner is null
            ? null
            : new Recommendation(winner.QuotationId, winner.TotalScore, $"Highest weighted score among {ranked.Count} eligible quotation(s).");

        AddDomainEvent(new SimulationCompleted(Id, ScenarioId, winner?.QuotationId, InputFingerprint.Value, DateTimeOffset.UtcNow));
    }

    public QuotationEvaluation GetEvaluation(string quotationId) =>
        _evaluations.FirstOrDefault(evaluation => evaluation.QuotationId == quotationId)
            ?? throw new DomainException($"No evaluation found for quotation '{quotationId}' in this simulation run.");

    public bool IsBasedOn(InputFingerprint fingerprint) => InputFingerprint.Matches(fingerprint);
}

