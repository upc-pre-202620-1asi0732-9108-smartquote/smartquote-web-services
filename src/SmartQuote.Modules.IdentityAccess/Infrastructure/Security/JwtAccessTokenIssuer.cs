using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SmartQuote.Modules.IdentityAccess.Application.Ports;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;

namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Security;

public sealed class JwtAccessTokenIssuer(JwtTokenOptions options) : IAccessTokenIssuer
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(UserAccount account, DateTimeOffset now)
    {
        var expiresAt = now.AddMinutes(options.AccessTokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.Email, account.Email),
            new("name", account.DisplayName)
        };
        claims.AddRange(account.Roles.Select(role => new Claim("role", role.Role.ToString())));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
