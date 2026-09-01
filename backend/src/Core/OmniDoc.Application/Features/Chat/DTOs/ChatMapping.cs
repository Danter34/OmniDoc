using System.Linq.Expressions;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Application.Features.Chat.DTOs;

public static class ChatMapping
{
    public static readonly Expression<Func<Conversation, ConversationDto>> ConversationProjection =
        conversation => new ConversationDto(
            conversation.Id,
            conversation.WorkspaceId,
            conversation.Title,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc ?? conversation.CreatedAtUtc);

    public static readonly Expression<Func<ChatMessage, ChatMessageDto>> MessageProjection =
        message => new ChatMessageDto(
            message.Id,
            message.ConversationId,
            message.Role.ToString(),
            message.Content,
            message.CreatedAtUtc,
            message.Citations
                .Select(citation => new CitationDto(
                    citation.ChunkId,
                    citation.DocumentId,
                    citation.DocumentTitle,
                    citation.PageNumber,
                    citation.Excerpt,
                    citation.SimilarityScore))
                .ToList());

    public static ChatMessageDto ToDto(this ChatMessage message) => new(
        message.Id,
        message.ConversationId,
        message.Role.ToString(),
        message.Content,
        message.CreatedAtUtc,
        message.Citations
            .Select(citation => new CitationDto(
                citation.ChunkId,
                citation.DocumentId,
                citation.DocumentTitle,
                citation.PageNumber,
                citation.Excerpt,
                citation.SimilarityScore))
            .ToList());
}
