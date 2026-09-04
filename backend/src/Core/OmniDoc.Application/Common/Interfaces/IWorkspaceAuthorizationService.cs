using OmniDoc.Application.Common.Models;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Common.Interfaces;

public sealed record WorkspaceAuthorizationContext(
    Guid WorkspaceId,
    Guid UserId,
    WorkspaceRole Role);

public interface IWorkspaceAuthorizationService
{
    Task<Result<WorkspaceAuthorizationContext>> AuthorizeAsync(
        Guid workspaceId,
        WorkspacePermission permission,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceAuthorizationContext>> AuthorizeAsync(
        Guid workspaceId,
        Guid userId,
        WorkspacePermission permission,
        CancellationToken cancellationToken = default);
}
