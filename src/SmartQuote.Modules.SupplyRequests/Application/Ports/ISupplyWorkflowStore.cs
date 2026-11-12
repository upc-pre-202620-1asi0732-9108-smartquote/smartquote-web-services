using SmartQuote.API.SupplyRequests.Domain.Model.Aggregates;

namespace SmartQuote.API.SupplyRequests.Application.Ports;

public interface IRequestUserDirectory
{
    Task<bool> IsActiveInRoleAsync(Guid userId, string role, CancellationToken ct = default);
}

public interface IScheduleLease : IAsyncDisposable
{
    RecurringRequestSchedule Schedule { get; }
    Task CommitAsync(CancellationToken ct);
}

public interface ISupplyWorkflowStore
{
    Task<TechnicalClarification?> GetClarificationAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TechnicalClarification>> ListClarificationsAsync(Guid? requestId, Guid? recipientId, string? status, int page, int pageSize, CancellationToken ct);
    void AddClarification(TechnicalClarification clarification);
    void AddSchedule(RecurringRequestSchedule schedule);
    Task<IReadOnlyList<RecurringRequestSchedule>> ListSchedulesAsync(Guid owner, int page, int pageSize, CancellationToken ct);
    Task<RecurringRequestSchedule?> GetScheduleAsync(Guid id, CancellationToken ct);
    Task<RecurringRequestSchedule?> GetScheduleForOccurrenceAsync(Guid occurrenceId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetDueScheduleIdsAsync(DateTimeOffset now, int limit, CancellationToken ct);
    Task<IScheduleLease> LockScheduleAsync(Guid id, CancellationToken ct);
}
