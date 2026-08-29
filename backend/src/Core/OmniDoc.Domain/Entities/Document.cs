using OmniDoc.Domain.Common;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Domain.Entities;

public class Document : BaseAuditableEntity
{
    public Guid WorkspaceId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public DocumentStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public int ChunkCount { get; set; }

    public Workspace? Workspace { get; set; }

    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
