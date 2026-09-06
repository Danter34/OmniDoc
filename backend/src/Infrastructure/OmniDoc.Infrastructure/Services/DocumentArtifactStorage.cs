using System.Security.Cryptography;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Infrastructure.Services;

public sealed class DocumentArtifactStorage(IFileStorageService files) : IDocumentArtifactStorage
{
    public async Task<DocumentArtifact> SaveAsync(Stream stream, Guid workspaceId, Guid documentId,
        ArtifactKind kind, string fileName, string contentType, string producer, CancellationToken ct)
    {
        var start = stream.Position;
        var size = stream.Length - start;
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
        stream.Position = start;
        var path = await files.SaveFileAsync(stream, fileName, workspaceId, ct);
        return new DocumentArtifact
        {
            DocumentId = documentId, Kind = kind, FileName = Path.GetFileName(fileName),
            ContentType = contentType, FileSizeBytes = size, StoragePath = path, Sha256 = hash, Producer = producer
        };
    }

    public Task<Stream?> OpenAsync(DocumentArtifact artifact, CancellationToken ct) => files.GetFileAsync(artifact.StoragePath, ct);
}
