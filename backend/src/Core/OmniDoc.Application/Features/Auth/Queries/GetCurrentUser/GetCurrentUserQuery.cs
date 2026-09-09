using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Auth.DTOs;

namespace OmniDoc.Application.Features.Auth.Queries.GetCurrentUser;

public record GetCurrentUserQuery : IRequest<Result<UserDto>>;

public sealed class GetCurrentUserQueryHandler
    : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetCurrentUserQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<UserDto>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<UserDto>.Failure("Bạn không có quyền thực hiện thao tác này.", 401);
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new UserDto(
                item.Id,
                item.Email,
                item.FullName,
                item.CreatedAtUtc,
                item.EmailConfirmed,
                item.EmailConfirmed
                    ? null
                    : item.LastOtpSentAt.HasValue
                        ? item.LastOtpSentAt.Value.AddSeconds(60)
                        : null))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? Result<UserDto>.Failure("Không tìm thấy tài khoản người dùng.", 404)
            : Result<UserDto>.Success(user);
    }
}
