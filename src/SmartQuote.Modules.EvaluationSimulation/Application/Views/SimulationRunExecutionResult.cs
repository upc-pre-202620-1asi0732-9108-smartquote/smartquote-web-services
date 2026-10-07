using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Application.Views;

public sealed record SimulationRunExecutionResult(SimulationRunId RunId, bool WasCreated);
