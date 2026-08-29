using OmniDoc.Domain.Common;

namespace OmniDoc.Domain.Entities;

public class DocumentChunk : BaseAuditableEntity
{
    public Guid DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public string? BoundingBoxJson { get; set; }

    public Document? Document { get; set; }
}
