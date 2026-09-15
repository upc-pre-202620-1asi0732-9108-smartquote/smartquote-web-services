using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Entities;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Enums;

namespace SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;

public sealed class UserAccount : AggregateRoot<UserId>
{
    private readonly List<UserRole> _roles = [];
    private readonly List<RefreshSession> _refreshSessions = [];

    private UserAccount()
    {
    }

    public UserAccount(
        UserId id,
        string email,
        string displayName,
        string passwordHash,
        IEnumerable<SmartQuoteRole> roles,
        DateTimeOffset createdAt)
    {
        var normalizedEmail = NormalizeEmail(email);
        var assignedRoles = roles.Distinct().ToList();
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 150)
            throw new ArgumentException("Display name must contain up to 150 characters.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (assignedRoles.Count == 0)
            throw new ArgumentException("At least one role is required.", nameof(roles));

        Id = id;
        Email = email.Trim();
        NormalizedEmail = normalizedEmail;
        DisplayName = displayName.Trim();
        PasswordHash = passwordHash;
        Status = AccountStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        _roles.AddRange(assignedRoles.Select(role => new UserRole(role)));
    }

    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public AccountStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();
    public IReadOnlyCollection<RefreshSession> RefreshSessions => _refreshSessions.AsReadOnly();

    public bool IsActive => Status == AccountStatus.Active;

    public void AddRefreshSession(RefreshSession refreshSession)
    {
        ArgumentNullException.ThrowIfNull(refreshSession);
        _refreshSessions.Add(refreshSession);
    }

    public void ChangePassword(string passwordHash, DateTimeOffset changedAt)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        PasswordHash = passwordHash;
        UpdatedAt = changedAt;
    }

    public void Disable(DateTimeOffset changedAt)
    {
        Status = AccountStatus.Disabled;
        UpdatedAt = changedAt;
        foreach (var session in _refreshSessions)
            session.Revoke(changedAt);
    }

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        var value = email.Trim();
        if (value.Length > 254 || !value.Contains('@', StringComparison.Ordinal))
            throw new ArgumentException("Email format is invalid.", nameof(email));

        return value.ToUpperInvariant();
    }
}
