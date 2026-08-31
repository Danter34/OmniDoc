using OmniDoc.Domain.Common;

namespace OmniDoc.Domain.Entities;

public class Workspace : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid OwnerId { get; set; }

    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();

    public ICollection<Document> Documents { get; set; } = new List<Document>();

    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
