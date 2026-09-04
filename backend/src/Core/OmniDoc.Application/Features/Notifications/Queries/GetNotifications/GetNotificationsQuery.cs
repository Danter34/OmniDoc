using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Notifications.DTOs;

namespace OmniDoc.Application.Features.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<NotificationPageDto>>;

public sealed class GetNotificationsQueryValidator : AbstractValidator<GetNotificationsQuery>
{
    public GetNotificationsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, Result<NotificationPageDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<NotificationPageDto>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<NotificationPageDto>.Failure("Authentication is required.", 401);
        }

        var query = _context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var items = entities.Select(NotificationDto.FromEntity).ToList();

        return Result<NotificationPageDto>.Success(new NotificationPageDto(
            items,
            request.Page,
            request.PageSize,
            totalCount));
    }
}
