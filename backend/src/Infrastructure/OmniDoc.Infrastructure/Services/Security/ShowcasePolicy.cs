using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Exceptions;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.Infrastructure.Services.Security;

public sealed class ShowcasePolicy(IOptions<ShowcaseSettings> options) : IShowcasePolicy
{
    public const string DisabledMessage = "Chức năng này bị vô hiệu hóa trên tài khoản trải nghiệm công khai.";
    public const string ShowcaseDisabledLoginMessage = "Tài khoản trải nghiệm đã bị vô hiệu hóa.";
    private readonly ShowcaseSettings _settings = options.Value;

    public bool IsEnabled => _settings.Enabled;

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

    public void EnsureCanSignIn(Guid userId, string? email = null)
    {
        var matchesUser = userId != Guid.Empty && userId == _settings.UserId;
        var matchesEmail = !string.IsNullOrWhiteSpace(email) &&
                           string.Equals(email.Trim(), _settings.Email.Trim(), StringComparison.OrdinalIgnoreCase);

        if (!_settings.Enabled && (matchesUser || matchesEmail))
        {
            throw new ForbiddenException(ShowcaseDisabledLoginMessage);
        }
    }
}
