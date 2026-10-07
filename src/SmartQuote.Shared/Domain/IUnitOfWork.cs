namespace SmartQuote.API.Shared.Domain;

public interface IUnitOfWork
{
    Task CompleteAsync(CancellationToken cancellationToken = default);
}
