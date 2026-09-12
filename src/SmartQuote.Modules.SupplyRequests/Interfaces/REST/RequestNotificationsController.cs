using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.SupplyRequests.Application;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

namespace SmartQuote.API.SupplyRequests.Interfaces.REST;

[ApiController]
[Authorize(Roles = SmartQuoteRoles.ProductionSpecialist)]
[Route("api/v1/notifications")]
public sealed class RequestNotificationsController(RequestNotificationService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RequestNotificationResource>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RequestNotificationResource>>> GetMine(
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var notifications = await service.GetMineAsync(unreadOnly, cancellationToken);
        return Ok(notifications.Select(item => new RequestNotificationResource(
            item.NotificationId,
            item.PurchaseRequestId,
            item.NewStatus,
            item.Message,
            item.CreatedAt,
            item.ReadAt)).ToList());
    }

    [HttpPut("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await service.MarkMineAsReadAsync(notificationId, cancellationToken);
        return NoContent();
    }
}
