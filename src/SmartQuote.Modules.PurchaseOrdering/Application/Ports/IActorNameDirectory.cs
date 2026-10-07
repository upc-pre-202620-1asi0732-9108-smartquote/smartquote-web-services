namespace SmartQuote.API.PurchaseOrdering.Application.Ports;

public interface IActorNameDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IEnumerable<Guid> actorIds,
        CancellationToken cancellationToken = default);
}
