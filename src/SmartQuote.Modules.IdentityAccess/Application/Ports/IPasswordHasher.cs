namespace SmartQuote.Modules.IdentityAccess.Application.Ports;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}
