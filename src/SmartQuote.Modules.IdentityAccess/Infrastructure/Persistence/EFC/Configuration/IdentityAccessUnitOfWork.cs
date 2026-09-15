using SmartQuote.Modules.IdentityAccess.Application.Ports;

namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Configuration;

public sealed class IdentityAccessUnitOfWork(IdentityAccessDbContext context) : IIdentityAccessUnitOfWork
{
    public Task CompleteAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
