namespace SmartQuote.Modules.IdentityAccess.Application.Views;

public sealed record AuthenticatedSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    CurrentUserView User);
