using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Common.Interfaces;

public interface IDocumentArtifactStorage
{
    Task<DocumentArtifact> SaveAsync(Stream stream, Guid workspaceId, Guid documentId,
        ArtifactKind kind, string fileName, string contentType, string producer, CancellationToken ct);
    Task<Stream?> OpenAsync(DocumentArtifact artifact, CancellationToken ct);
}
