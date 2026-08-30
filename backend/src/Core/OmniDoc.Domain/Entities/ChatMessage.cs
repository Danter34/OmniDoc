using OmniDoc.Domain.Common;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Domain.Entities;

public class ChatMessage : BaseAuditableEntity
{
    public Guid ConversationId { get; set; }

    public MessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public Conversation? Conversation { get; set; }

    public ICollection<Citation> Citations { get; set; } = new List<Citation>();
}
