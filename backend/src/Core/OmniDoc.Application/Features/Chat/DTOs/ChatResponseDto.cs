namespace OmniDoc.Application.Features.Chat.DTOs;

public record ChatResponseDto(
    Guid ConversationId,
    ChatMessageDto UserMessage,
    ChatMessageDto AssistantMessage);
