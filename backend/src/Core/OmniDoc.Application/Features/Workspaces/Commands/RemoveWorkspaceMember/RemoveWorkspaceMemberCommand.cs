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
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public RemoveWorkspaceMemberCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
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
            return Result<bool>.Failure("Authentication is required.", 401);
        }

        var workspace = await _context.Workspaces
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == request.WorkspaceId, cancellationToken);

        if (workspace is null)
        {
            return Result<bool>.Failure(
                $"Workspace '{request.WorkspaceId}' was not found.",
                404);
        }

        var actor = workspace.Members.FirstOrDefault(member => member.UserId == actorUserId);
        if (actor is null)
        {
            return Result<bool>.Failure(
                "You do not have access to this workspace.",
                403);
        }

        var targetMember = workspace.Members
            .FirstOrDefault(member => member.UserId == request.MemberUserId);

        if (targetMember is null)
        {
            return Result<bool>.Failure("Workspace member was not found.", 404);
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
                    "Workspace admins can only remove members.",
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
                    "A workspace must always have at least one owner.",
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
