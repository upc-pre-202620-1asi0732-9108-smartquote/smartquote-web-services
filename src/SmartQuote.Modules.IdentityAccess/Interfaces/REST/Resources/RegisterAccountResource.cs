using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SmartQuote.Modules.IdentityAccess.Domain.Services;

namespace SmartQuote.Modules.IdentityAccess.Interfaces.REST.Resources;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RegisterAccountResource
{
    private string _email = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get => _email; init => _email = value?.Trim() ?? string.Empty; }

    [Required, StringLength(150, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    [Required, StringLength(RegistrationPasswordPolicy.MaximumLength,
        MinimumLength = RegistrationPasswordPolicy.MinimumLength)]
    public string Password { get; init; } = string.Empty;

    [StringLength(40)]
    public string? Role { get; init; }
}
