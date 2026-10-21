using Microsoft.EntityFrameworkCore;
using SmartQuote.Modules.IdentityAccess.Application.OutboundServices;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Enums;
using SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Repositories;

public sealed class UserDirectory(IdentityAccessDbContext db) : IUserDirectory
{
    public async Task<DirectoryUser?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.UserAccounts.AsNoTracking().Include(u => u.Roles).SingleOrDefaultAsync(u => u.Id == new UserId(userId), ct);
        return user is null ? null : new(user.Id.Value, user.DisplayName, user.Email, user.IsActive, user.Roles.Select(r => r.Role.ToString()).ToList());
    }
    public async Task<IReadOnlyList<DirectoryUser>> ListActiveAnalystsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Invalid pagination: page >= 1 and pageSize 1–100 are required.");
        var users = await db.UserAccounts.AsNoTracking().Include(u => u.Roles)
            .Where(u => u.Status == AccountStatus.Active && u.Roles.Any(r => r.Role == SmartQuoteRole.PurchaseAnalyst))
            .OrderBy(u => u.DisplayName).ThenBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return users.Select(u => new DirectoryUser(u.Id.Value, u.DisplayName, u.Email, true, u.Roles.Select(r => r.Role.ToString()).ToList())).ToList();
    }
}
