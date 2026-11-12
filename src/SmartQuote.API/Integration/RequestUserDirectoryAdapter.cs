using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.Modules.IdentityAccess.Application.OutboundServices;

namespace SmartQuote.API.Integration;

public sealed class RequestUserDirectoryAdapter(IUserDirectory directory) : IRequestUserDirectory
{
    public async Task<bool> IsActiveInRoleAsync(Guid userId, string role, CancellationToken ct = default)
    {
        var user = await directory.GetAsync(userId, ct);
        return user is { IsActive: true } && user.Roles.Contains(role, StringComparer.Ordinal);
    }
}
