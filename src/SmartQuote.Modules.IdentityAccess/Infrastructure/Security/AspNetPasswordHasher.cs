using Microsoft.AspNetCore.Identity;
using SmartQuote.Modules.IdentityAccess.Application.Ports;

namespace SmartQuote.Modules.IdentityAccess.Infrastructure.Security;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new object(), password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(new object(), passwordHash, password)
        is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
