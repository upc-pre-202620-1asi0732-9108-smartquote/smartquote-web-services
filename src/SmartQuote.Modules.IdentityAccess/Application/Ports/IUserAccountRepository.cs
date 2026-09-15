using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;

namespace SmartQuote.Modules.IdentityAccess.Application.Ports;

public interface IUserAccountRepository
{
    Task<UserAccount?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByIdAsync(UserId userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserAccount account, CancellationToken cancellationToken = default);
}
