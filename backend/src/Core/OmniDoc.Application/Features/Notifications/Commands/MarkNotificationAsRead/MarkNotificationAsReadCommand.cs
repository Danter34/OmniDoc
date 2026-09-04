using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Notifications.DTOs;

namespace OmniDoc.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public sealed record MarkNotificationAsReadCommand(Guid NotificationId)
    : IRequest<Result<NotificationDto>>;

public sealed class MarkNotificationAsReadCommandHandler
    : IRequestHandler<MarkNotificationAsReadCommand, Result<NotificationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public MarkNotificationAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<NotificationDto>> Handle(
        MarkNotificationAsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<NotificationDto>.Failure("Authentication is required.", 401);
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(
                item => item.Id == request.NotificationId && item.UserId == userId,
                cancellationToken);

        if (notification is null)
        {
            return Result<NotificationDto>.Failure("Notification was not found.", 404);
        }

        if (notification.MarkAsRead(_timeProvider.GetUtcNow().UtcDateTime))
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result<NotificationDto>.Success(NotificationDto.FromEntity(notification));
    }
}
