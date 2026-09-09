using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Workspaces.Commands.RemoveWorkspaceMember;

public sealed record RemoveWorkspaceMemberCommand(
    Guid WorkspaceId,
    Guid MemberUserId) : IRequest<Result<bool>>;

public sealed class RemoveWorkspaceMemberCommandHandler
    : IRequestHandler<RemoveWorkspaceMemberCommand, Result<bool>>
{
    private readonly IShowcasePolicy _showcase;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public RemoveWorkspaceMemberCommandHandler(
        IShowcasePolicy showcase,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _showcase = showcase;
        _context = context;
        _currentUser = currentUser;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<bool>> Handle(
        RemoveWorkspaceMemberCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } actorUserId)
        {
            return Result<bool>.Failure("Bạn không có quyền thực hiện thao tác này.", 401);
        }

        _showcase.EnsureCanModifyAccount(actorUserId);
        _showcase.EnsureCanModifyWorkspace(request.WorkspaceId);

        var workspace = await _context.Workspaces
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == request.WorkspaceId, cancellationToken);

        if (workspace is null)
        {
            return Result<bool>.Failure(
                $"Không tìm thấy không gian làm việc '{request.WorkspaceId}'.",
                404);
        }

        var actor = workspace.Members.FirstOrDefault(member => member.UserId == actorUserId);
        if (actor is null)
        {
            return Result<bool>.Failure(
                "Bạn không có quyền thực hiện thao tác này.",
                403);
        }

        var targetMember = workspace.Members
            .FirstOrDefault(member => member.UserId == request.MemberUserId);

        if (targetMember is null)
        {
            return Result<bool>.Failure("Không tìm thấy thành viên trong không gian làm việc.", 404);
        }

        var isSelfRemoval = actorUserId == request.MemberUserId;
        if (!isSelfRemoval)
        {
            var access = await _workspaceAuthorization.AuthorizeAsync(
                request.WorkspaceId,
                WorkspacePermission.RemoveMembers,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<bool>.Failure(access.Errors, access.StatusCode);
            }

            if (access.Data!.Role == WorkspaceRole.Admin &&
                targetMember.Role != WorkspaceRole.Member)
            {
                return Result<bool>.Failure(
                    "Quản trị viên chỉ có thể xóa người dùng có vai trò thành viên.",
                    403);
            }
        }

        if (targetMember.Role == WorkspaceRole.Owner)
        {
            var otherOwner = workspace.Members.FirstOrDefault(member =>
                member.UserId != targetMember.UserId &&
                member.Role == WorkspaceRole.Owner);

            if (otherOwner is null)
            {
                return Result<bool>.Failure(
                    "Không gian làm việc phải luôn có ít nhất một chủ sở hữu.",
                    409);
            }

            if (workspace.OwnerId == targetMember.UserId)
            {
                workspace.OwnerId = otherOwner.UserId;
            }
        }

        _context.WorkspaceMembers.Remove(targetMember);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
