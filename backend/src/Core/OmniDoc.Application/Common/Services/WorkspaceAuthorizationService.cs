using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Domain.Authorization;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Common.Services;

public sealed class WorkspaceAuthorizationService : IWorkspaceAuthorizationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public WorkspaceAuthorizationService(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<WorkspaceAuthorizationContext>> AuthorizeAsync(
        Guid workspaceId,
        WorkspacePermission permission,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<WorkspaceAuthorizationContext>.Failure(
                "Authentication is required.",
                401);
        }

        return await AuthorizeAsync(
            workspaceId,
            userId,
            permission,
            cancellationToken);
    }

    public async Task<Result<WorkspaceAuthorizationContext>> AuthorizeAsync(
        Guid workspaceId,
        Guid userId,
        WorkspacePermission permission,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _context.Workspaces
            .AsNoTracking()
            .Where(item => item.Id == workspaceId)
            .Select(item => new
            {
                item.OwnerId,
                Role = item.Members
                    .Where(member => member.UserId == userId)
                    .Select(member => (WorkspaceRole?)member.Role)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (workspace is null)
        {
            return Result<WorkspaceAuthorizationContext>.Failure(
                $"Workspace '{workspaceId}' was not found.",
                404);
        }

        var role = workspace.OwnerId == userId
            ? WorkspaceRole.Owner
            : workspace.Role;

        if (role is null)
        {
            return Result<WorkspaceAuthorizationContext>.Failure(
                "You do not have access to this workspace.",
                403);
        }

        if (!WorkspacePermissionMatrix.HasPermission(role.Value, permission))
        {
            return Result<WorkspaceAuthorizationContext>.Failure(
                $"Workspace permission '{permission}' is required.",
                403);
        }

        return Result<WorkspaceAuthorizationContext>.Success(
            new WorkspaceAuthorizationContext(workspaceId, userId, role.Value));
    }
}
