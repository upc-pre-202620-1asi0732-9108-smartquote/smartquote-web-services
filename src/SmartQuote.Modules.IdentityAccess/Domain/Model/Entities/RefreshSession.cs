namespace SmartQuote.Modules.IdentityAccess.Domain.Model.Entities;

public sealed class RefreshSession
{
    private RefreshSession()
    {
    }

    public RefreshSession(string tokenHash, DateTimeOffset expiresAt, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Refresh token hash is required.", nameof(tokenHash));
        if (expiresAt <= createdAt)
            throw new ArgumentException("Refresh token expiry must be in the future.", nameof(expiresAt));

        Id = Guid.NewGuid();
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (RevokedAt is null)
            RevokedAt = revokedAt;
    }
}
