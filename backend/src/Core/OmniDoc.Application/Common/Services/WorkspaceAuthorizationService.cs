using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Domain.Authorization;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Common.Services;

public sealed class WorkspaceAuthorizationService : IWorkspaceAuthorizationService
{
    private readonly IShowcasePolicy _showcase;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public WorkspaceAuthorizationService(
        IShowcasePolicy showcase,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _showcase = showcase;
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
                "Bạn không có quyền thực hiện thao tác này.",
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
        if (_showcase.IsShowcaseUser(userId) && !_showcase.IsShowcaseWorkspace(workspaceId))
            return Result<WorkspaceAuthorizationContext>.Failure("Tài khoản trải nghiệm chỉ có thể truy cập không gian làm việc mẫu.", 403);
        if (permission != WorkspacePermission.ViewWorkspace)
        {
            _showcase.EnsureCanModifyAccount(userId);
            _showcase.EnsureCanModifyWorkspace(workspaceId);
        }

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
                $"Không tìm thấy không gian làm việc '{workspaceId}'.",
                404);
        }

        var role = workspace.OwnerId == userId
            ? WorkspaceRole.Owner
            : workspace.Role;

        if (role is null)
        {
            return Result<WorkspaceAuthorizationContext>.Failure(
                "Bạn không có quyền thực hiện thao tác này.",
                403);
        }

        if (!WorkspacePermissionMatrix.HasPermission(role.Value, permission))
        {
            return Result<WorkspaceAuthorizationContext>.Failure(
                "Bạn không có quyền thực hiện thao tác này.",
                403);
        }

        return Result<WorkspaceAuthorizationContext>.Success(
            new WorkspaceAuthorizationContext(workspaceId, userId, role.Value));
    }
}
