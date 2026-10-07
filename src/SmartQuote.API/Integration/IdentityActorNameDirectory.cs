using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.Modules.IdentityAccess.Application;

namespace SmartQuote.API.Integration;

// Composition-root adapter: lets the audit trail show who acted without
// Purchase Ordering referencing the IdentityAccess module directly.
public sealed class IdentityActorNameDirectory(AuthenticationService authentication) : IActorNameDirectory
{
    public Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IEnumerable<Guid> actorIds,
        CancellationToken cancellationToken = default) =>
        authentication.GetDisplayNamesAsync(actorIds, cancellationToken);
}
