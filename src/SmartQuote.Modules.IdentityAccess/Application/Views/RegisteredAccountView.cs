namespace SmartQuote.Modules.IdentityAccess.Application.Views;

public sealed record RegisteredAccountView(
    Guid UserId,
    string Email,
    string DisplayName,
    string Status,
    IReadOnlyList<string> Roles,
    bool InitialSetup);
