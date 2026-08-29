namespace OmniDoc.Application.Features.Documents.DTOs;

public record DocumentDto(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string Status,
    string? ErrorMessage,
    int ChunkCount,
    DateTime CreatedAtUtc);
