using System.ComponentModel.DataAnnotations;

namespace SmartQuote.Modules.IdentityAccess.Interfaces.REST.Resources;

public sealed class LoginResource
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 1)]
    public string Password { get; init; } = string.Empty;
}
