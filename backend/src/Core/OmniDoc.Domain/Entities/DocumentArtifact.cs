using OmniDoc.Domain.Enums;

namespace OmniDoc.Domain.Entities;

public sealed class DocumentArtifact
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DocumentId { get; init; }
    public ArtifactKind Kind { get; init; }
    public int Generation { get; init; } = 1;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string StoragePath { get; init; } = string.Empty;
    // Empty only for legacy artifacts: a SQL migration cannot hash external files.
    public string Sha256 { get; init; } = string.Empty;
    public string Producer { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
