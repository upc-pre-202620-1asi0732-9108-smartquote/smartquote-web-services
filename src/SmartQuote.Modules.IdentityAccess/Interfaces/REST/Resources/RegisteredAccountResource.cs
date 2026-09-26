namespace SmartQuote.Modules.IdentityAccess.Interfaces.REST.Resources;

public sealed record RegisteredAccountResource(
    Guid UserId,
    string Email,
    string DisplayName,
    string Status,
    IReadOnlyList<string> Roles,
    bool InitialSetup);
