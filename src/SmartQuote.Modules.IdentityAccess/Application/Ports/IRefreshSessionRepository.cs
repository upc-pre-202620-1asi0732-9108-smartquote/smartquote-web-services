using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Entities;

namespace SmartQuote.Modules.IdentityAccess.Application.Ports;

public interface IRefreshSessionRepository
{
    Task<(UserAccount Account, RefreshSession Session)?> GetActiveByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);
}
