using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Chat.Services;

/// Loads the tail of a conversation as prompt messages. Shared by the synchronous and the
/// streaming chat paths so both send the model the same window of history.
public static class ChatHistoryReader
{
    public static async Task<List<ChatPromptMessage>> LoadRecentHistoryAsync(
        this IApplicationDbContext context,
        Guid conversationId,
        int limit = RagPromptBuilder.HistoryMessageLimit,
        CancellationToken cancellationToken = default)
    {
        var recent = await context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Role != MessageRole.System)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(limit)
            .Select(m => new { m.Role, m.Content, m.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        return recent
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new ChatPromptMessage(m.Role.ToString(), m.Content))
            .ToList();
    }
}
