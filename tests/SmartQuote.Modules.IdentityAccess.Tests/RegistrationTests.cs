using System.Text.Json;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.IdentityAccess.Application;
using SmartQuote.Modules.IdentityAccess.Application.Commands;
using SmartQuote.Modules.IdentityAccess.Application.Ports;
using SmartQuote.Modules.IdentityAccess.Application.Views;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Entities;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Enums;
using SmartQuote.Modules.IdentityAccess.Domain.Services;
using SmartQuote.Modules.IdentityAccess.Infrastructure.Security;
using SmartQuote.Modules.IdentityAccess.Interfaces.REST.Resources;
using Xunit;

namespace SmartQuote.Modules.IdentityAccess.Tests;

public sealed class RegistrationTests
{
    private const string ValidPassword = "SecureComplex93!";

    // US09/E1: First Account Is Automatically The Initial Purchase Manager; comprobación aislada.
    [Fact]
    [Trait("Story", "US09"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public async Task FirstAccountIsAutomaticallyTheInitialPurchaseManager()
    {
        var fixture = new Fixture();
        var status = await fixture.Service.GetRegistrationStatusAsync();

        Assert.True(status.InitialSetupRequired);
        var registered = await fixture.Service.RegisterAsync(
            new RegisterAccountCommand("manager@example.com", "First Manager", ValidPassword, null));

        Assert.True(registered.InitialSetup);
        Assert.Equal("Active", registered.Status);
        Assert.Equal("PurchaseManager", Assert.Single(registered.Roles));
        Assert.False((await fixture.Service.GetRegistrationStatusAsync()).InitialSetupRequired);
    }

    // US09/E1: Subsequent Accounts Remain Pending Until Manager Approval; comprobación aislada.
    [Theory]
    [InlineData("ProductionSpecialist")]
    [InlineData("PurchaseAnalyst")]
    [InlineData("PurchaseManager")]
    [Trait("Story", "US09"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public async Task SubsequentAccountsRemainPendingUntilManagerApproval(string requestedRole)
    {
        var fixture = new Fixture();
        await fixture.RegisterInitialManager();
        var registered = await fixture.Service.RegisterAsync(new RegisterAccountCommand(
            "new.user@example.com", "New User", ValidPassword, requestedRole));

        Assert.False(registered.InitialSetup);
        Assert.Equal("Pending", registered.Status);
        Assert.Equal(requestedRole, Assert.Single(registered.Roles));
        await Assert.ThrowsAsync<AuthenticationException>(() => fixture.Service.LoginAsync(
            new LoginCommand("new.user@example.com", ValidPassword)));

        var pending = Assert.Single(await fixture.Service.GetPendingRegistrationsAsync());
        Assert.Equal(requestedRole, pending.RequestedRole);
        var activated = await fixture.Service.ApproveRegistrationAsync(
            registered.UserId, "PurchaseAnalyst");

        Assert.Equal("PurchaseAnalyst", Assert.Single(activated.Roles));
        var session = await fixture.Service.LoginAsync(new LoginCommand("new.user@example.com", ValidPassword));
        Assert.Equal("PurchaseAnalyst", Assert.Single(session.User.Roles));
    }

    // US09/E1: Unknown Roles Are Rejected After Initial Setup; comprobación aislada.
    [Theory]
    [InlineData("Administrator")]
    [InlineData("2")]
    [Trait("Story", "US09"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public async Task UnknownRolesAreRejectedAfterInitialSetup(string role)
    {
        var fixture = new Fixture();
        await fixture.RegisterInitialManager();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.RegisterAsync(new RegisterAccountCommand(
            "new.user@example.com", "New User", ValidPassword, role)));
        Assert.Single(fixture.Accounts.Items);
    }

    // US09/E2: Weak Password Does Not Create Account; comprobación aislada.
    [Theory]
    [InlineData("TooShort7!")]
    [InlineData("lowercaseonly93!")]
    [InlineData("UPPERCASEONLY93!")]
    [InlineData("NoNumberIncluded!")]
    [InlineData("NoSymbolIncluded93")]
    [InlineData("New.UserStrong93!")]
    [Trait("Story", "US09"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public async Task WeakPasswordDoesNotCreateAccount(string password)
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.RegisterAsync(
            new RegisterAccountCommand("new.user@example.com", "New User", password, null)));

        Assert.Empty(fixture.Accounts.Items);
    }

    // US09/E2: Password Policy Rejects Control Characters And Allows Strong Passphrase; comprobación aislada.
    [Fact]
    [Trait("Story", "US09"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public void PasswordPolicyRejectsControlCharactersAndAllowsStrongPassphrase()
    {
        Assert.Throws<ArgumentException>(() =>
            RegistrationPasswordPolicy.Validate("StrongPassword93!\n", "user@example.com"));
        Assert.Throws<ArgumentException>(() =>
            RegistrationPasswordPolicy.Validate(new string('A', 126) + "a9!", "user@example.com"));

        RegistrationPasswordPolicy.Validate("A long, Secure passphrase 93!", "user@example.com");
    }

    // US09/E2: Password Policy Accepts Both Length Limits And Rejects Values Outside Them; comprobación aislada.
    [Fact]
    [Trait("Story", "US09"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public void PasswordPolicyAcceptsBothLengthLimitsAndRejectsValuesOutsideThem()
    {
        var minimum = "Aa123456789!";
        var maximum = "Aa" + new string('7', 125) + "!";

        Assert.Equal(RegistrationPasswordPolicy.MinimumLength, minimum.Length);
        Assert.Equal(RegistrationPasswordPolicy.MaximumLength, maximum.Length);
        RegistrationPasswordPolicy.Validate(minimum, "user@example.com");
        RegistrationPasswordPolicy.Validate(maximum, "user@example.com");

        Assert.Throws<ArgumentException>(() =>
            RegistrationPasswordPolicy.Validate(minimum[..^1], "user@example.com"));
        Assert.Throws<ArgumentException>(() =>
            RegistrationPasswordPolicy.Validate(maximum + "7", "user@example.com"));
    }

    // US09/E2: Registered Email Is Unique Ignoring Case And Surrounding Spaces; comprobación aislada.
    [Fact]
    [Trait("Story", "US09"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public async Task RegisteredEmailIsUniqueIgnoringCaseAndSurroundingSpaces()
    {
        var fixture = new Fixture();
        await fixture.RegisterInitialManager();
        await fixture.Service.RegisterAsync(new RegisterAccountCommand(
            "New.User@Example.com", "New User", ValidPassword, "ProductionSpecialist"));

        var exception = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.RegisterAsync(
            new RegisterAccountCommand(" new.user@example.com ", "Another User", ValidPassword, "PurchaseAnalyst")));

        Assert.Contains("Email", exception.Message);
        Assert.Equal(2, fixture.Accounts.Items.Count);
    }

    // US09/E1: Initial Setup Cannot Self Assign Another Role; comprobación aislada.
    [Fact]
    [Trait("Story", "US09"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public async Task InitialSetupCannotSelfAssignAnotherRole()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.RegisterAsync(new RegisterAccountCommand(
            "first@example.com", "First User", ValidPassword, "ProductionSpecialist")));

        Assert.Empty(fixture.Accounts.Items);
    }

    // US09/E2: Registration Request Rejects Unexpected Fields; comprobación aislada.
    [Fact]
    [Trait("Story", "US09"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public void RegistrationRequestRejectsUnexpectedFields()
    {
        const string json = """
            {"Email":"new.user@example.com","DisplayName":"New User","Password":"SecureComplex93!","Role":"PurchaseManager","InvitationCode":"old-flow"}
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RegisterAccountResource>(json));
    }

    private sealed class Fixture
    {
        public readonly FakeAccountRepository Accounts = new();
        public AuthenticationService Service { get; }

        public Fixture() => Service = new AuthenticationService(
            Accounts,
            new FakeRefreshSessionRepository(),
            new AspNetPasswordHasher(),
            new FakeAccessTokenIssuer(),
            new FakeRefreshTokenGenerator(),
            new FakeUnitOfWork());

        public Task<RegisteredAccountView> RegisterInitialManager() => Service.RegisterAsync(
            new RegisterAccountCommand("manager@example.com", "Initial Manager", ValidPassword, null));
    }

    private sealed class FakeAccountRepository : IUserAccountRepository
    {
        public readonly List<UserAccount> Items = [];

        public Task<UserAccount?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(account => account.NormalizedEmail == normalizedEmail));

        public Task<UserAccount?> GetByIdAsync(UserId userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(account => account.Id == userId));

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items.Count > 0);

        public Task<IReadOnlyList<UserAccount>> FindPendingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserAccount>>(Items.Where(account => account.Status == AccountStatus.Pending).ToList());

        public Task AddAsync(UserAccount account, CancellationToken cancellationToken = default)
        {
            Items.Add(account);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRefreshSessionRepository : IRefreshSessionRepository
    {
        public Task<(UserAccount Account, RefreshSession Session)?> GetActiveByTokenHashAsync(
            string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult<(UserAccount Account, RefreshSession Session)?>(null);
    }

    private sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
    {
        public (string Token, DateTimeOffset ExpiresAt) Issue(UserAccount account, DateTimeOffset now) =>
            ($"access-token-{account.Id.Value}", now.AddMinutes(30));
    }

    private sealed class FakeRefreshTokenGenerator : IRefreshTokenGenerator
    {
        public string Generate() => "refresh-token";
        public string Hash(string rawToken) => rawToken;
    }

    private sealed class FakeUnitOfWork : IIdentityAccessUnitOfWork
    {
        public Task BeginRegistrationAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AbortRegistrationAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
