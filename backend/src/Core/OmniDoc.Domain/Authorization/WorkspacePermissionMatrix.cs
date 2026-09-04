using OmniDoc.Domain.Enums;

namespace OmniDoc.Domain.Authorization;

public static class WorkspacePermissionMatrix
{
    public static bool HasPermission(
        WorkspaceRole role,
        WorkspacePermission permission)
    {
        if (!Enum.IsDefined(permission))
        {
            return false;
        }

        return role switch
        {
            WorkspaceRole.Owner => true,
            WorkspaceRole.Admin => permission is
                WorkspacePermission.ViewWorkspace or
                WorkspacePermission.ManageDocuments or
                WorkspacePermission.InviteMembers or
                WorkspacePermission.RemoveMembers,
            WorkspaceRole.Member => permission is
                WorkspacePermission.ViewWorkspace or
                WorkspacePermission.ManageDocuments,
            _ => false
        };
    }
}
