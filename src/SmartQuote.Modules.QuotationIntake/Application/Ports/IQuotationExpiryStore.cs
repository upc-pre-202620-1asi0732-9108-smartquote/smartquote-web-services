using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;

namespace SmartQuote.Modules.QuotationIntake.Application.Ports;

public record ExpiringQuotation(Guid QuotationId, Guid RequestId, string SupplierName, DateOnly ValidUntil);
public record AlertRequestState(bool IsActive, bool IsAwarded, Guid? AssignedAnalystId);
public interface IAlertRequestReader { Task<AlertRequestState?> ReadAsync(Guid requestId, CancellationToken ct); }
public interface IQuotationExpiryStore
{
    Task<IReadOnlyList<ExpiringQuotation>> CandidatesAsync(DateOnly today, DateOnly through, int skip, int batchSize, CancellationToken ct);
    Task UpsertAsync(ExpiringQuotation quote, Guid recipient, int leadDays, DateOnly today, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<QuotationExpiryAlert>> ListAsync(Guid recipient, bool unreadOnly, int page, int pageSize, CancellationToken ct);
    Task<QuotationExpiryAlert?> FindAsync(Guid id, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
