using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Domain.Repositories;

namespace SmartQuote.API.Shared.Infrastructure.Persistence;

public abstract class BaseRepository<TAggregate, TContext>(TContext context) : IBaseRepository<TAggregate>
    where TAggregate : class
    where TContext : DbContext
{
    protected readonly TContext Context = context;

    public async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default) =>
        await Context.Set<TAggregate>().AddAsync(aggregate, cancellationToken);
}
