using OmniDoc.Domain.Enums;

namespace OmniDoc.Domain.Entities;

public class WorkspaceInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkspaceId { get; set; }

    public Guid InviterId { get; set; }

    public string InviteeEmail { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public WorkspaceRole Role { get; set; }

    public DateTime ExpiresAt { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Workspace? Workspace { get; set; }

    public User? Inviter { get; set; }
}
