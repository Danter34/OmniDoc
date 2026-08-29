using OmniDoc.Domain.Common;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Domain.Entities;

public class WorkspaceMember : BaseAuditableEntity
{
    public Guid WorkspaceId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public WorkspaceRole Role { get; set; }

    public Workspace? Workspace { get; set; }
}
