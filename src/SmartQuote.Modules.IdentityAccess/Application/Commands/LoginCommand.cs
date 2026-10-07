namespace SmartQuote.Modules.IdentityAccess.Application.Commands;

public sealed record LoginCommand(string Email, string Password);
