using SmartQuote.API.PurchaseOrdering.Application.Ports;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;

public sealed class PurchaseOrderingUnitOfWork(PurchaseOrderingDbContext context) : IPurchaseOrderingUnitOfWork
{
    public Task CompleteAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
