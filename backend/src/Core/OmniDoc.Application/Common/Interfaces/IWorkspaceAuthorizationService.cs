using OmniDoc.Application.Common.Models;

namespace OmniDoc.Application.Common.Interfaces;

public interface IWorkspaceAuthorizationService
{
    Task<Result> AuthorizeAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    Task<Result> AuthorizeAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> AuthorizeOwnerAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
