using OmniDoc.Domain.Enums;

namespace OmniDoc.Domain.Entities;

public class WorkspaceMember
{
    public Guid WorkspaceId { get; set; }

    public Guid UserId { get; set; }

    public WorkspaceRole Role { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    public Workspace? Workspace { get; set; }

    public User? User { get; set; }
}
