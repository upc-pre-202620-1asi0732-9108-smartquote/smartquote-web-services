using System.Security.Cryptography;
using System.Text;
using SmartQuote.Modules.IdentityAccess.Application.Ports;

namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Security;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
