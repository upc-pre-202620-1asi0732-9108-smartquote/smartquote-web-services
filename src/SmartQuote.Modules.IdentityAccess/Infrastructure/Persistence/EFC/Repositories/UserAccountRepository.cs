using Microsoft.EntityFrameworkCore;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.IdentityAccess.Application.Ports;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;
using SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Repositories;

public sealed class UserAccountRepository(IdentityAccessDbContext context) : IUserAccountRepository
{
    public Task<UserAccount?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        AccountsWithRoles().FirstOrDefaultAsync(account => account.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<UserAccount?> GetByIdAsync(UserId userId, CancellationToken cancellationToken = default) =>
        AccountsWithRoles().FirstOrDefaultAsync(account => account.Id == userId, cancellationToken);

    public Task AddAsync(UserAccount account, CancellationToken cancellationToken = default) =>
        context.UserAccounts.AddAsync(account, cancellationToken).AsTask();

    private IQueryable<UserAccount> AccountsWithRoles() =>
        context.UserAccounts.Include(account => account.Roles);
}
