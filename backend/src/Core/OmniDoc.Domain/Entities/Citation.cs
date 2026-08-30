using OmniDoc.Domain.Common;

namespace OmniDoc.Domain.Entities;

public class Citation : BaseAuditableEntity
{
    public Guid ChatMessageId { get; set; }

    public Guid ChunkId { get; set; }

    public Guid DocumentId { get; set; }

    public string DocumentTitle { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public string Excerpt { get; set; } = string.Empty;

    public float SimilarityScore { get; set; }

    public ChatMessage? ChatMessage { get; set; }
}
