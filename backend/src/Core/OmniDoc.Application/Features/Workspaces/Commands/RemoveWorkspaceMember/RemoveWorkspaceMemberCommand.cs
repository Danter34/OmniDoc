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

    public RemoveWorkspaceMemberCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
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
        if (actor.Role != WorkspaceRole.Owner && !isSelfRemoval)
        {
            return Result<bool>.Failure(
                "Workspace owner permission is required to remove another member.",
                403);
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
