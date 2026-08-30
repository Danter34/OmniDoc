namespace OmniDoc.Application.Features.Chat.DTOs;

public record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    string Role,
    string Content,
    DateTime CreatedAtUtc,
    List<CitationDto> Citations);
