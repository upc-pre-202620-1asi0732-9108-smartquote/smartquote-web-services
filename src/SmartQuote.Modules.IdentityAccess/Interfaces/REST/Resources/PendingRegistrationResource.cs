namespace SmartQuote.Modules.IdentityAccess.Interfaces.REST.Resources;

public sealed record PendingRegistrationResource(
    Guid UserId,
    string Email,
    string DisplayName,
    string RequestedRole,
    DateTimeOffset CreatedAt);
