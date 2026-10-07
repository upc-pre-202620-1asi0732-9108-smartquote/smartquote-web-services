namespace SmartQuote.Modules.IdentityAccess.Application.Views;

public sealed record CurrentUserView(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);
