namespace SmartQuote.Modules.IdentityAccess.Application.Commands;

public sealed record RegisterAccountCommand(
    string Email,
    string DisplayName,
    string Password,
    string? Role);
