using SmartQuote.API.SupplyRequests.Application.Ports;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;

public sealed class SupplyRequestsUnitOfWork(SupplyRequestsDbContext context) : ISupplyRequestsUnitOfWork
{
    public Task CompleteAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
