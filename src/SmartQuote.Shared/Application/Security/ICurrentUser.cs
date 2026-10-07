namespace SmartQuote.API.Shared.Application.Security;

public interface ICurrentUser
{
    Guid UserId { get; }
    IReadOnlySet<string> Roles { get; }
    bool IsInRole(string role);
}
