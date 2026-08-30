namespace OmniDoc.Application.Features.Chat.DTOs;

public record CitationDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentTitle,
    int PageNumber,
    string Excerpt);
