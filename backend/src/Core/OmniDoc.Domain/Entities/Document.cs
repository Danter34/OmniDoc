using OmniDoc.Domain.Common;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Domain.Entities;

public class Document : BaseAuditableEntity
{
    public DocumentFormat DetectedFormat { get; set; }
    public Guid? SourceArtifactId { get; private set; }
    public Guid? CanonicalArtifactId { get; private set; }
    public ProcessingStage ProcessingStage { get; set; }
    public int ProgressPercentage { get; set; }
    public string? FailureCode { get; set; }
    public ICollection<DocumentArtifact> Artifacts { get; private set; } = new List<DocumentArtifact>();

    public void AddArtifact(DocumentArtifact artifact)
    {
        if (artifact.DocumentId != Id || artifact.Id == Guid.Empty || artifact.Generation < 1 ||
            artifact.FileSizeBytes <= 0 || string.IsNullOrWhiteSpace(artifact.StoragePath) ||
            string.IsNullOrWhiteSpace(artifact.FileName) || !Enum.IsDefined(artifact.Kind))
            throw new InvalidOperationException("Invalid artifact or document ownership.");
        if (artifact.Kind == ArtifactKind.Source && SourceArtifactId is not null)
            throw new InvalidOperationException("The source artifact is immutable.");
        if (artifact.Kind == ArtifactKind.CanonicalPdf &&
            (SourceArtifactId is null || CanonicalArtifactId is not null ||
             artifact.ContentType != "application/pdf" ||
             !artifact.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Canonical PDF requires a source and cannot be replaced.");
        if (Artifacts.Any(a => a.Id == artifact.Id || (a.Kind == artifact.Kind && a.Generation == artifact.Generation)))
            throw new InvalidOperationException("Duplicate artifact generation.");
        Artifacts.Add(artifact);
        if (artifact.Kind == ArtifactKind.Source) SourceArtifactId = artifact.Id;
        else CanonicalArtifactId = artifact.Id;
    }

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
