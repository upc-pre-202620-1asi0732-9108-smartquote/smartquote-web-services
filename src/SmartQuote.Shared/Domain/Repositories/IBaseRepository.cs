namespace SmartQuote.API.Shared.Domain.Repositories;

public interface IBaseRepository<TAggregate> where TAggregate : class
{
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}
