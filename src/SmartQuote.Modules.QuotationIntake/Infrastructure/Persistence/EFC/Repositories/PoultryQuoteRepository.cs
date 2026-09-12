using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Infrastructure.Persistence;
using SmartQuote.Modules.QuotationIntake.Application.Ports;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Enums;
using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;
using SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Repositories;

public class PoultryQuoteRepository(QuotationIntakeDbContext context)
    : BaseRepository<PoultryQuote, QuotationIntakeDbContext>(context), IPoultryQuoteRepository
{
    public async Task<PoultryQuote?> GetByIdAsync(PoultryQuoteId quotationId, CancellationToken cancellationToken = default) =>
        await Context.PoultryQuotes
            .AsSplitQuery()
            .Include(quote => quote.Lines)
            .ThenInclude(line => line.Specifications)
            .Include(quote => quote.Fields)
            .ThenInclude(field => field.Corrections)
            .FirstOrDefaultAsync(quote => quote.Id == quotationId, cancellationToken);

    public async Task<PoultryQuote?> FindByRequestAndHashAsync(string requestId, string sha256Hash, CancellationToken cancellationToken = default)
    {
        var reference = new PurchaseRequestReference(requestId);

        return await Context.PoultryQuotes
            .Where(quote => quote.RequestReference == reference)
            .Where(quote => quote.SourceDocument.Sha256Hash == sha256Hash)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PoultryQuote>> GetVerifiedForRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        var reference = new PurchaseRequestReference(requestId);

        return await Context.PoultryQuotes
            .AsSplitQuery()
            .Include(quote => quote.Lines)
            .ThenInclude(line => line.Specifications)
            .Where(quote => quote.RequestReference == reference)
            .Where(quote => quote.Status == QuotationStatus.Verified)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PoultryQuote>> GetForRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        var reference = new PurchaseRequestReference(requestId);
        return await Context.PoultryQuotes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(quote => quote.Lines)
            .ThenInclude(line => line.Specifications)
            .Include(quote => quote.Fields)
            .ThenInclude(field => field.Corrections)
            .Where(quote => quote.RequestReference == reference)
            .OrderByDescending(quote => quote.UpdatedAt)
            .ToListAsync(cancellationToken);
    }
}
