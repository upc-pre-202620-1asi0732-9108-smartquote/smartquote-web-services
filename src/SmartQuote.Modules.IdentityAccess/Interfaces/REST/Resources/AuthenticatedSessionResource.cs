namespace SmartQuote.Modules.IdentityAccess.Interfaces.REST.Resources;

public sealed record AuthenticatedSessionResource(
    string AccessToken,
    string TokenType,
    long ExpiresIn,
    CurrentUserResource User);
