using System.Security.Claims;
using SmartQuote.API.Shared.Application.Security;

namespace SmartQuote.API.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User
                                         ?? throw new UnauthorizedAccessException("An authenticated user is required.");

    public Guid UserId
    {
        get
        {
            var subject = Principal.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? Principal.FindFirstValue("sub")
                          ?? throw new UnauthorizedAccessException("The access token does not contain a subject identifier.");

            return Guid.TryParse(subject, out var userId)
                ? userId
                : throw new UnauthorizedAccessException("The access token subject is not a valid UUID.");
        }
    }

    public IReadOnlySet<string> Roles => Principal.FindAll("role")
        .Select(claim => claim.Value)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsInRole(string role) => Principal.IsInRole(role);
}
