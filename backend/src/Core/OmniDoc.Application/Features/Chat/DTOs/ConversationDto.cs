namespace OmniDoc.Application.Features.Chat.DTOs;

public record ConversationDto(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    DateTime CreatedAtUtc,
    DateTime LastActivityAtUtc);
