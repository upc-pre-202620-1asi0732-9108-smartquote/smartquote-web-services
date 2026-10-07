using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartQuote.Modules.IdentityAccess.Interfaces.REST.Resources;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ApproveRegistrationResource
{
    [Required, StringLength(40)]
    public string Role { get; init; } = string.Empty;
}
