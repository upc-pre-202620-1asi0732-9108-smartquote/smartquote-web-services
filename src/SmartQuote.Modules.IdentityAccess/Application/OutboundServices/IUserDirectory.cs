namespace SmartQuote.Modules.IdentityAccess.Application.OutboundServices;

public sealed record DirectoryUser(Guid UserId, string DisplayName, string Email, bool IsActive, IReadOnlyList<string> Roles);
public interface IUserDirectory
{
    Task<DirectoryUser?> GetAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<DirectoryUser>> ListActiveAnalystsAsync(int page, int pageSize, CancellationToken ct = default);
}
