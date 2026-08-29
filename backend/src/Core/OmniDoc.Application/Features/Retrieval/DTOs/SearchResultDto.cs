namespace OmniDoc.Application.Features.Retrieval.DTOs;

public record SearchResultDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentTitle,
    int PageNumber,
    string Content,
    float SimilarityScore);
