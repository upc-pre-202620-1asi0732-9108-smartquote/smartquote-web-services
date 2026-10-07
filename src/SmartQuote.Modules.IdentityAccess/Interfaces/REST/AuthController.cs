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
public sealed class AuthController(AuthenticationService authenticationService) : ControllerBase
{
    private const string RefreshCookieName = "smartquote_refresh";

    [AllowAnonymous]
    [HttpGet("api/v1/iam/auth/registration-status")]
    [ProducesResponseType<RegistrationStatusResource>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RegistrationStatusResource>> RegistrationStatus(CancellationToken cancellationToken)
    {
        var status = await authenticationService.GetRegistrationStatusAsync(cancellationToken);
        return Ok(new RegistrationStatusResource(status.InitialSetupRequired));
    }

    [AllowAnonymous]
    [EnableRateLimiting("authentication-register")]
    [HttpPost("api/v1/iam/auth/register")]
    [ProducesResponseType<RegisteredAccountResource>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RegisteredAccountResource>> Register(
        [FromBody] RegisterAccountResource resource,
        CancellationToken cancellationToken)
    {
        var account = await authenticationService.RegisterAsync(
            new RegisterAccountCommand(resource.Email, resource.DisplayName, resource.Password, resource.Role),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ToResource(account));
    }

    [Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
    [HttpGet("api/v1/iam/registration-requests")]
    [ProducesResponseType<IReadOnlyList<PendingRegistrationResource>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PendingRegistrationResource>>> PendingRegistrations(
        CancellationToken cancellationToken)
    {
        var requests = await authenticationService.GetPendingRegistrationsAsync(cancellationToken);
        return Ok(requests.Select(ToResource).ToList());
    }

    [Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
    [HttpPost("api/v1/iam/registration-requests/{userId:guid}/approve")]
    [ProducesResponseType<CurrentUserResource>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CurrentUserResource>> ApproveRegistration(
        Guid userId,
        [FromBody] ApproveRegistrationResource resource,
        CancellationToken cancellationToken)
    {
        var user = await authenticationService.ApproveRegistrationAsync(userId, resource.Role, cancellationToken);
        return Ok(ToResource(user));
    }

    [Authorize(Roles = SmartQuoteRoles.PurchaseManager)]
    [HttpPost("api/v1/iam/registration-requests/{userId:guid}/reject")]
    [ProducesResponseType<CurrentUserResource>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CurrentUserResource>> RejectRegistration(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await authenticationService.RejectRegistrationAsync(userId, cancellationToken);
        return Ok(ToResource(user));
    }

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
    public async Task<ActionResult<CurrentUserResource>> Me(
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
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

    private static RegisteredAccountResource ToResource(RegisteredAccountView view) => new(
        view.UserId, view.Email, view.DisplayName, view.Status, view.Roles, view.InitialSetup);

    private static PendingRegistrationResource ToResource(PendingRegistrationView view) => new(
        view.UserId, view.Email, view.DisplayName, view.RequestedRole, view.CreatedAt);
}
