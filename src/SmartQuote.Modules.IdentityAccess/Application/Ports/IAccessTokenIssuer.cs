using SmartQuote.Modules.IdentityAccess.Application.Views;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;

namespace SmartQuote.Modules.IdentityAccess.Application.Ports;

public interface IAccessTokenIssuer
{
    (string Token, DateTimeOffset ExpiresAt) Issue(UserAccount account, DateTimeOffset now);
}
