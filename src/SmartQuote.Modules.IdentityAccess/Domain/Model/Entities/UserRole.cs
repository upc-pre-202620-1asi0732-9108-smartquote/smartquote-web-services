using SmartQuote.Modules.IdentityAccess.Domain.Model.Enums;

namespace SmartQuote.Modules.IdentityAccess.Domain.Model.Entities;

public sealed class UserRole
{
    private UserRole()
    {
    }

    public UserRole(SmartQuoteRole role)
    {
        Id = Guid.NewGuid();
        Role = role;
    }

    public Guid Id { get; private set; }
    public SmartQuoteRole Role { get; private set; }
}
