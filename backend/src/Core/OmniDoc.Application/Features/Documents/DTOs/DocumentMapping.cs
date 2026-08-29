using System.Linq.Expressions;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Application.Features.Documents.DTOs;

public static class DocumentMapping
{
    public static readonly Expression<Func<Document, DocumentDto>> Projection = document => new DocumentDto(
        document.Id,
        document.WorkspaceId,
        document.Title,
        document.FileName,
        document.ContentType,
        document.FileSizeBytes,
        document.Status.ToString(),
        document.ErrorMessage,
        document.ChunkCount,
        document.CreatedAtUtc);

    public static DocumentDto ToDto(this Document document) => new(
        document.Id,
        document.WorkspaceId,
        document.Title,
        document.FileName,
        document.ContentType,
        document.FileSizeBytes,
        document.Status.ToString(),
        document.ErrorMessage,
        document.ChunkCount,
        document.CreatedAtUtc);
}
