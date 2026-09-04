using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Notifications.DTOs;

namespace OmniDoc.Application.Features.Notifications.Commands.MarkAllNotificationsAsRead;

public sealed record MarkAllNotificationsAsReadCommand
    : IRequest<Result<NotificationCountDto>>;

public sealed class MarkAllNotificationsAsReadCommandHandler
    : IRequestHandler<MarkAllNotificationsAsReadCommand, Result<NotificationCountDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public MarkAllNotificationsAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<NotificationCountDto>> Handle(
        MarkAllNotificationsAsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<NotificationCountDto>.Failure("Authentication is required.", 401);
        }

        var notifications = await _context.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToListAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var notification in notifications)
        {
            notification.MarkAsRead(now);
        }

        if (notifications.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result<NotificationCountDto>.Success(
            new NotificationCountDto(notifications.Count));
    }
}
