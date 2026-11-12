using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;

namespace SmartQuote.Modules.EvaluationSimulation.Application.Ports;

public interface IEvaluationLease : IAsyncDisposable { Task CommitAsync(CancellationToken ct); }
public interface IAdvancedEvaluationStore
{
    Task<IEvaluationLease> LockAsync(CancellationToken ct);
    void Add(SensitivityAnalysis analysis);
    Task<SensitivityAnalysis?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SensitivityAnalysis>> ListAsync(string requestId, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
