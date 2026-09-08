using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Exceptions;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.Infrastructure.Services.Security;

public sealed class ShowcasePolicy(IOptions<ShowcaseSettings> options) : IShowcasePolicy
{
    public const string DisabledMessage = "Chức năng này bị vô hiệu hóa trên tài khoản trải nghiệm công khai.";
    private readonly ShowcaseSettings _settings = options.Value;

    public bool IsShowcaseUser(Guid userId) => _settings.Enabled && userId != Guid.Empty && userId == _settings.UserId;
    public bool IsShowcaseWorkspace(Guid workspaceId) => _settings.Enabled && workspaceId != Guid.Empty && workspaceId == _settings.WorkspaceId;

    public void EnsureCanModifyAccount(Guid userId)
    {
        if (IsShowcaseUser(userId)) throw new ForbiddenException(DisabledMessage);
    }

    public void EnsureCanModifyWorkspace(Guid workspaceId)
    {
        if (IsShowcaseWorkspace(workspaceId)) throw new ForbiddenException(DisabledMessage);
    }

    public void EnsureCanDeleteConversation(Guid workspaceId) => EnsureCanModifyWorkspace(workspaceId);
}
