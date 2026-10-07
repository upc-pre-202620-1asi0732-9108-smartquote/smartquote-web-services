using System.ComponentModel.DataAnnotations;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.IdentityAccess.Application.Commands;
using SmartQuote.Modules.IdentityAccess.Application.Ports;
using SmartQuote.Modules.IdentityAccess.Application.Views;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Entities;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Enums;
using SmartQuote.Modules.IdentityAccess.Domain.Services;

namespace SmartQuote.Modules.IdentityAccess.Application;

public sealed class AuthenticationService(
    IUserAccountRepository userAccountRepository,
    IRefreshSessionRepository refreshSessionRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IIdentityAccessUnitOfWork unitOfWork)
{
    public async Task<RegistrationStatusView> GetRegistrationStatusAsync(CancellationToken cancellationToken = default) =>
        new(!await userAccountRepository.AnyAsync(cancellationToken));

    public async Task<RegisteredAccountView> RegisterAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeRegistrationEmail(command.Email);
        RegistrationPasswordPolicy.Validate(command.Password, command.Email);

        await unitOfWork.BeginRegistrationAsync(cancellationToken);
        try
        {
            if (await userAccountRepository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken) is not null)
                throw EmailAlreadyRegistered();

            var initialSetup = !await userAccountRepository.AnyAsync(cancellationToken);
            var role = initialSetup
                ? ParseInitialSetupRole(command.Role)
                : ParseRequestedRole(command.Role);
            var status = initialSetup ? AccountStatus.Active : AccountStatus.Pending;
            var now = DateTimeOffset.UtcNow;
            var account = new UserAccount(
                new UserId(Guid.NewGuid()),
                command.Email,
                command.DisplayName,
                passwordHasher.Hash(command.Password),
                [role],
                now,
                status);

            await userAccountRepository.AddAsync(account, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);
            return ToRegistrationView(account, initialSetup);
        }
        catch
        {
            await unitOfWork.AbortRegistrationAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<PendingRegistrationView>> GetPendingRegistrationsAsync(
        CancellationToken cancellationToken = default)
    {
        var pending = await userAccountRepository.FindPendingAsync(cancellationToken);
        return pending.Select(account => new PendingRegistrationView(
            account.Id.Value,
            account.Email,
            account.DisplayName,
            account.Roles.Single().Role.ToString(),
            account.CreatedAt)).ToList();
    }

    public async Task<CurrentUserView> ApproveRegistrationAsync(
        Guid userId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseRole(roleName, out var role))
            throw InvalidRole();

        var account = await userAccountRepository.GetByIdAsync(new UserId(userId), cancellationToken)
            ?? throw new KeyNotFoundException("Registration request was not found.");
        account.ApproveRegistration(role, DateTimeOffset.UtcNow);
        await unitOfWork.CompleteAsync(cancellationToken);
        return ToView(account);
    }

    public async Task<CurrentUserView> RejectRegistrationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var account = await userAccountRepository.GetByIdAsync(new UserId(userId), cancellationToken)
            ?? throw new KeyNotFoundException("Registration request was not found.");
        account.RejectRegistration(DateTimeOffset.UtcNow);
        await unitOfWork.CompleteAsync(cancellationToken);
        return ToView(account);
    }

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

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var userId in userIds.Distinct())
        {
            var account = await userAccountRepository.GetByIdAsync(new UserId(userId), cancellationToken);
            if (account is not null)
                names[userId] = account.DisplayName;
        }
        return names;
    }

    public async Task<CurrentUserView> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await userAccountRepository.GetByIdAsync(new UserId(userId), cancellationToken)
            ?? throw new AuthenticationException("The authenticated account is not available.");
        if (!account.IsActive)
            throw new AuthenticationException("The authenticated account is not active.");

        return ToView(account);
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

    private static RegisteredAccountView ToRegistrationView(UserAccount account, bool initialSetup) => new(
        account.Id.Value,
        account.Email,
        account.DisplayName,
        account.Status.ToString(),
        account.Roles.Select(role => role.Role.ToString()).ToList(),
        initialSetup);

    private static CurrentUserView ToView(UserAccount account) => new(
        account.Id.Value,
        account.Email,
        account.DisplayName,
        account.Roles.Select(role => role.Role.ToString()).OrderBy(role => role).ToList());

    private static SmartQuoteRole ParseInitialSetupRole(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName) || roleName == SmartQuoteRole.PurchaseManager.ToString())
            return SmartQuoteRole.PurchaseManager;
        throw InvalidRole();
    }

    private static SmartQuoteRole ParseRequestedRole(string? roleName) =>
        TryParseRole(roleName, out var role) ? role : throw InvalidRole();

    private static bool TryParseRole(string? roleName, out SmartQuoteRole role) =>
        Enum.TryParse(roleName, out role) && Enum.IsDefined(role) &&
        string.Equals(roleName, role.ToString(), StringComparison.Ordinal);

    private static ArgumentException InvalidRole() =>
        new("Role must be ProductionSpecialist, PurchaseAnalyst, or PurchaseManager.", "role");

    private static AuthenticationException InvalidCredentials() =>
        new("Email or password is invalid.");

    private static ConflictException EmailAlreadyRegistered() =>
        new("Email is already registered.");

    private static string NormalizeRegistrationEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email.Trim()))
            throw new ArgumentException("Email format is invalid.", nameof(email));

        return UserAccount.NormalizeEmail(email);
    }
}
