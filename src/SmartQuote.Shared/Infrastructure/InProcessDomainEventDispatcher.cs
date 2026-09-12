using Microsoft.Extensions.DependencyInjection;
using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.Shared.Infrastructure;

/// <summary>
/// Resolves IDomainEventHandler&lt;TEvent&gt; instances from DI for each raised event.
/// A minimal, dependency-free stand-in for a mediator, since none is in the project's stack.
/// </summary>
public class InProcessDomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());

            foreach (var handler in serviceProvider.GetServices(handlerType))
            {
                if (handler is null)
                    continue;

                var handleTask = (Task)handlerType
                    .GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!
                    .Invoke(handler, [domainEvent, cancellationToken])!;

                await handleTask;
            }
        }
    }
}
