using OmniDoc.Domain.Common;

namespace OmniDoc.Domain.Entities;

public class Conversation : BaseAuditableEntity
{
    public Guid WorkspaceId { get; set; }

    public string Title { get; set; } = string.Empty;

    public Workspace? Workspace { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
