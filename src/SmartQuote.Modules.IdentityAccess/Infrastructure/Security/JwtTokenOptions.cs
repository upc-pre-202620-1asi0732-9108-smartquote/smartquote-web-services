namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Security;

public sealed class JwtTokenOptions
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; init; } = 10;
}
