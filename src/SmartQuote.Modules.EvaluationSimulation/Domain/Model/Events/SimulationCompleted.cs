using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.Events;

public record SimulationCompleted(
    SimulationRunId SimulationRunId,
    EvaluationScenarioId ScenarioId,
    string? RecommendedQuotationId,
    string InputFingerprint,
    DateTimeOffset OccurredAt) : IDomainEvent;
