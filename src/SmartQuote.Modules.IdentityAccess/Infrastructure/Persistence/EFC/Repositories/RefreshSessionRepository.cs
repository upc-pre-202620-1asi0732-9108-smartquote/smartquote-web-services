using Microsoft.EntityFrameworkCore;
using SmartQuote.Modules.IdentityAccess.Application.Ports;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Entities;
using SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Repositories;

public sealed class RefreshSessionRepository(IdentityAccessDbContext context) : IRefreshSessionRepository
{
    public async Task<(UserAccount Account, RefreshSession Session)?> GetActiveByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        var account = await context.UserAccounts
            .AsSplitQuery()
            .Include(item => item.Roles)
            .Include(item => item.RefreshSessions)
            .FirstOrDefaultAsync(
                item => item.RefreshSessions.Any(session => session.TokenHash == tokenHash),
                cancellationToken);
        if (account is null)
            return null;

        var session = account.RefreshSessions.SingleOrDefault(item => item.TokenHash == tokenHash);
        return session is null ? null : (account, session);
    }
}
