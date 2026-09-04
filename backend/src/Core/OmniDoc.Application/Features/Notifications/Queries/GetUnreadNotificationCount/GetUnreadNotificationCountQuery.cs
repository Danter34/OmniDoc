using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Notifications.DTOs;

namespace OmniDoc.Application.Features.Notifications.Queries.GetUnreadNotificationCount;

public sealed record GetUnreadNotificationCountQuery
    : IRequest<Result<NotificationCountDto>>;

public sealed class GetUnreadNotificationCountQueryHandler
    : IRequestHandler<GetUnreadNotificationCountQuery, Result<NotificationCountDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUnreadNotificationCountQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<NotificationCountDto>> Handle(
        GetUnreadNotificationCountQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<NotificationCountDto>.Failure("Authentication is required.", 401);
        }

        var count = await _context.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.UserId == userId && !notification.IsRead,
                cancellationToken);

        return Result<NotificationCountDto>.Success(new NotificationCountDto(count));
    }
}
