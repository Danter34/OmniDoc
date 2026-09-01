namespace OmniDoc.Application.Features.Chat.DTOs;

public record CitationDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    int PageNumber,
    string Snippet,
    float SimilarityScore);
