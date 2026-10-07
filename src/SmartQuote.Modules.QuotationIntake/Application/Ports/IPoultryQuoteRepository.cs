using SmartQuote.API.Shared.Domain.Repositories;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;
using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.QuotationIntake.Application.Ports;

public interface IPoultryQuoteRepository : IBaseRepository<PoultryQuote>
{
    Task<PoultryQuote?> GetByIdAsync(PoultryQuoteId quotationId, CancellationToken cancellationToken = default);

    Task<PoultryQuote?> FindByRequestAndHashAsync(string requestId, string sha256Hash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PoultryQuote>> GetVerifiedForRequestAsync(string requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PoultryQuote>> GetForRequestAsync(string requestId, CancellationToken cancellationToken = default);
}
