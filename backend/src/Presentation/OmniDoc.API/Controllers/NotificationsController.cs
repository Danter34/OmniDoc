using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Notifications.Commands.MarkAllNotificationsAsRead;
using OmniDoc.Application.Features.Notifications.Commands.MarkNotificationAsRead;
using OmniDoc.Application.Features.Notifications.DTOs;
using OmniDoc.Application.Features.Notifications.Queries.GetNotifications;
using OmniDoc.Application.Features.Notifications.Queries.GetUnreadNotificationCount;

namespace OmniDoc.API.Controllers;

[Authorize]
public sealed class NotificationsController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<NotificationPageDto>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return HandleResult(await Sender.Send(
            new GetNotificationsQuery(page, pageSize),
            cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<NotificationCountDto>> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new GetUnreadNotificationCountQuery(),
            cancellationToken));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<ActionResult<NotificationDto>> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new MarkNotificationAsReadCommand(id),
            cancellationToken));
    }

    [HttpPatch("read-all")]
    public async Task<ActionResult<NotificationCountDto>> MarkAllAsRead(
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new MarkAllNotificationsAsReadCommand(),
            cancellationToken));
    }
}
