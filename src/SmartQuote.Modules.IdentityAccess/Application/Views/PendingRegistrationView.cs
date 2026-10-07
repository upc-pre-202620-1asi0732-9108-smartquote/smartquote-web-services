namespace SmartQuote.Modules.IdentityAccess.Application.Views;

public sealed record PendingRegistrationView(
    Guid UserId,
    string Email,
    string DisplayName,
    string RequestedRole,
    DateTimeOffset CreatedAt);
