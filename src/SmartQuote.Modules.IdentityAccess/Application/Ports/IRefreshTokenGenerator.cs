namespace SmartQuote.Modules.IdentityAccess.Application.Ports;

public interface IRefreshTokenGenerator
{
    string Generate();
    string Hash(string rawToken);
}
