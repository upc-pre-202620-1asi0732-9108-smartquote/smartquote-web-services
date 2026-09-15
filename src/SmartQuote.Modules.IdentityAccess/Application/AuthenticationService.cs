using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.IdentityAccess.Application.Commands;
using SmartQuote.Modules.IdentityAccess.Application.Ports;
using SmartQuote.Modules.IdentityAccess.Application.Views;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Entities;

namespace SmartQuote.Modules.IdentityAccess.Application;

public sealed class AuthenticationService(
    IUserAccountRepository userAccountRepository,
    IRefreshSessionRepository refreshSessionRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IIdentityAccessUnitOfWork unitOfWork)
{
    public async Task<AuthenticatedSession> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Password))
            throw InvalidCredentials();

        UserAccount? account;
        try
        {
            account = await userAccountRepository.FindByNormalizedEmailAsync(
                UserAccount.NormalizeEmail(command.Email), cancellationToken);
        }
        catch (ArgumentException)
        {
            throw InvalidCredentials();
        }

        if (account is null || !account.IsActive || !passwordHasher.Verify(account.PasswordHash, command.Password))
            throw InvalidCredentials();

        return await IssueSessionAsync(account, cancellationToken);
    }

    public async Task<AuthenticatedSession> RefreshAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            throw InvalidCredentials();

        var result = await refreshSessionRepository.GetActiveByTokenHashAsync(
            refreshTokenGenerator.Hash(rawRefreshToken), cancellationToken);
        if (result is null || !result.Value.Account.IsActive || !result.Value.Session.IsActive(DateTimeOffset.UtcNow))
            throw InvalidCredentials();

        result.Value.Session.Revoke(DateTimeOffset.UtcNow);
        return await IssueSessionAsync(result.Value.Account, cancellationToken);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            return;

        var result = await refreshSessionRepository.GetActiveByTokenHashAsync(
            refreshTokenGenerator.Hash(rawRefreshToken), cancellationToken);
        if (result is null)
            return;

        result.Value.Session.Revoke(DateTimeOffset.UtcNow);
        await unitOfWork.CompleteAsync(cancellationToken);
    }

    public async Task<CurrentUserView> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await userAccountRepository.GetByIdAsync(new UserId(userId), cancellationToken)
            ?? throw new AuthenticationException("The authenticated account is not available.");
        if (!account.IsActive)
            throw new AuthenticationException("The authenticated account is not active.");

        return ToView(account);
    }

    public async Task BootstrapAccountAsync(
        BootstrapAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Password.Length < 12)
            throw new ArgumentException("Bootstrap passwords must contain at least 12 characters.", nameof(command));

        var normalizedEmail = UserAccount.NormalizeEmail(command.Email);
        if (await userAccountRepository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken) is not null)
            return;

        var now = DateTimeOffset.UtcNow;
        var account = new UserAccount(
            new UserId(Guid.NewGuid()),
            command.Email,
            command.DisplayName,
            passwordHasher.Hash(command.Password),
            [command.Role],
            now);
        await userAccountRepository.AddAsync(account, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
    }

    private async Task<AuthenticatedSession> IssueSessionAsync(UserAccount account, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var accessToken = accessTokenIssuer.Issue(account, now);
        var refreshToken = refreshTokenGenerator.Generate();
        var refreshSession = new RefreshSession(
            refreshTokenGenerator.Hash(refreshToken),
            now.AddHours(8),
            now);

        account.AddRefreshSession(refreshSession);
        await unitOfWork.CompleteAsync(cancellationToken);

        return new AuthenticatedSession(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken,
            refreshSession.ExpiresAt,
            ToView(account));
    }

    private static CurrentUserView ToView(UserAccount account) => new(
        account.Id.Value,
        account.Email,
        account.DisplayName,
        account.Roles.Select(role => role.Role.ToString()).OrderBy(role => role).ToList());

    private static AuthenticationException InvalidCredentials() =>
        new("Email or password is invalid.");
}
