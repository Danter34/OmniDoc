using Microsoft.AspNetCore.SignalR;

namespace OmniDoc.API.Hubs;

public class DocumentProgressHub : Hub
{
    public const string ProgressEventName = "DocumentProgressUpdated";

    // Guid.ToString() is always lowercase, so client-supplied ids are normalised to match.
    public static string GroupName(string workspaceId) => $"workspace-{workspaceId.Trim().ToLowerInvariant()}";

    public static string GroupName(Guid workspaceId) => GroupName(workspaceId.ToString());

    public Task JoinWorkspace(string workspaceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(workspaceId));

    public Task LeaveWorkspace(string workspaceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(workspaceId));
}
