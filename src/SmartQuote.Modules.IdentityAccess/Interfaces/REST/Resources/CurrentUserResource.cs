namespace SmartQuote.Modules.IdentityAccess.Interfaces.REST.Resources;

public sealed record CurrentUserResource(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);
