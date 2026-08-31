using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;

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

    public async Task<Result> AuthorizeAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result.Failure("Authentication is required.", 401);
        }

        return await AuthorizeAsync(workspaceId, userId, cancellationToken);
    }

    public async Task<Result> AuthorizeAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _context.Workspaces
            .AsNoTracking()
            .Where(item => item.Id == workspaceId)
            .Select(item => new
            {
                item.OwnerId,
                IsMember = item.Members.Any(member => member.UserId == userId)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (workspace is null)
        {
            return Result.Failure($"Workspace '{workspaceId}' was not found.", 404);
        }

        return workspace.OwnerId == userId || workspace.IsMember
            ? Result.Success()
            : Result.Failure("You do not have access to this workspace.", 403);
    }
}
