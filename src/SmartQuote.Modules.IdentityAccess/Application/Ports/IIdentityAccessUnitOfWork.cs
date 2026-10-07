namespace SmartQuote.Modules.IdentityAccess.Application.Ports;

public interface IIdentityAccessUnitOfWork
{
    Task BeginRegistrationAsync(CancellationToken cancellationToken = default);
    Task AbortRegistrationAsync(CancellationToken cancellationToken = default);
    Task CompleteAsync(CancellationToken cancellationToken = default);
}
