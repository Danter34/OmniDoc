namespace OmniDoc.Application.Common.Interfaces;

public interface IShowcasePolicy
{
    bool IsShowcaseUser(Guid userId);
    bool IsShowcaseWorkspace(Guid workspaceId);
    void EnsureCanModifyAccount(Guid userId);
    void EnsureCanModifyWorkspace(Guid workspaceId);
    void EnsureCanDeleteConversation(Guid workspaceId);
}
