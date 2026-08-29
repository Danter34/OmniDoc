namespace OmniDoc.Application.Features.Documents.DTOs;

public record DocumentChunkDto(
    Guid Id,
    int ChunkIndex,
    int PageNumber,
    string Content);
