namespace SmartQuote.Modules.IdentityAccess.Application.Ports;

public interface IIdentityAccessUnitOfWork
{
    Task CompleteAsync(CancellationToken cancellationToken = default);
}
