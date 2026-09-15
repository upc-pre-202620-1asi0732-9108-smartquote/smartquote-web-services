using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.Modules.IdentityAccess.Application;
using SmartQuote.Modules.IdentityAccess.Application.Commands;
using SmartQuote.Modules.IdentityAccess.Application.Views;
using SmartQuote.Modules.IdentityAccess.Interfaces.REST.Resources;

namespace SmartQuote.Modules.IdentityAccess.Interfaces.REST;

[ApiController]
public sealed class AuthController(AuthenticationService authenticationService, ICurrentUser currentUser) : ControllerBase
{
    private const string RefreshCookieName = "smartquote_refresh";

    [AllowAnonymous]
    [EnableRateLimiting("authentication-login")]
    [HttpPost("api/v1/iam/auth/login")]
    [ProducesResponseType<AuthenticatedSessionResource>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthenticatedSessionResource>> Login(
        [FromBody] LoginResource resource,
        CancellationToken cancellationToken)
    {
        var session = await authenticationService.LoginAsync(
            new LoginCommand(resource.Email, resource.Password), cancellationToken);
        WriteRefreshCookie(session);
        return Ok(ToResource(session));
    }

    [AllowAnonymous]
    [HttpPost("api/v1/iam/auth/refresh")]
    [ProducesResponseType<AuthenticatedSessionResource>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedSessionResource>> Refresh(CancellationToken cancellationToken)
    {
        var session = await authenticationService.RefreshAsync(
            Request.Cookies[RefreshCookieName] ?? string.Empty, cancellationToken);
        WriteRefreshCookie(session);
        return Ok(ToResource(session));
    }

    [AllowAnonymous]
    [HttpPost("api/v1/iam/auth/logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authenticationService.LogoutAsync(Request.Cookies[RefreshCookieName], cancellationToken);
        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions());
        return NoContent();
    }

    [Authorize]
    [HttpGet("api/v1/iam/auth/me")]
    [ProducesResponseType<CurrentUserResource>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResource>> Me(CancellationToken cancellationToken)
    {
        var user = await authenticationService.GetCurrentUserAsync(currentUser.UserId, cancellationToken);
        return Ok(ToResource(user));
    }

    private void WriteRefreshCookie(AuthenticatedSession session) =>
        Response.Cookies.Append(RefreshCookieName, session.RefreshToken,
            RefreshCookieOptions(session.RefreshTokenExpiresAt));

    private CookieOptions RefreshCookieOptions(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
        IsEssential = true,
        Path = "/api/v1/iam/auth",
        Expires = expires
    };

    private static AuthenticatedSessionResource ToResource(AuthenticatedSession session) => new(
        session.AccessToken,
        "Bearer",
        Math.Max(0, (long)(session.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds),
        ToResource(session.User));

    private static CurrentUserResource ToResource(CurrentUserView view) => new(
        view.UserId, view.Email, view.DisplayName, view.Roles);
}
