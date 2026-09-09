using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;

namespace OmniDoc.API.Hubs;

[Authorize]
public class DocumentProgressHub : Hub
{
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public DocumentProgressHub(IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _workspaceAuthorization = workspaceAuthorization;
    }

    public const string ProgressEventName = "DocumentProgressUpdated";

    // Guid.ToString() is always lowercase, so client-supplied ids are normalised to match.
    public static string GroupName(string workspaceId) => $"workspace-{workspaceId.Trim().ToLowerInvariant()}";

    public static string GroupName(Guid workspaceId) => GroupName(workspaceId.ToString());

    public async Task JoinWorkspace(string workspaceId)
    {
        if (!Guid.TryParse(workspaceId, out var parsedWorkspaceId) ||
            !Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            throw new HubException("Mã không gian làm việc hoặc tài khoản người dùng không hợp lệ.");
        }

        var access = await _workspaceAuthorization.AuthorizeAsync(
            parsedWorkspaceId,
            userId,
            WorkspacePermission.ViewWorkspace,
            Context.ConnectionAborted);

        if (!access.IsSuccess)
        {
            throw new HubException(access.Error ?? "Bạn không có quyền thực hiện thao tác này.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(parsedWorkspaceId),
            Context.ConnectionAborted);
    }

    public Task LeaveWorkspace(string workspaceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(workspaceId));
}
