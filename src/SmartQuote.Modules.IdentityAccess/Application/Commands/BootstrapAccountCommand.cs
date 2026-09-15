using SmartQuote.Modules.IdentityAccess.Domain.Model.Enums;

namespace SmartQuote.Modules.IdentityAccess.Application.Commands;

public sealed record BootstrapAccountCommand(
    string Email,
    string DisplayName,
    SmartQuoteRole Role,
    string Password);
