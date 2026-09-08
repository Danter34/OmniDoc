using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Chat.Services;

public static class ConversationNaming
{
    public static string BuildTitle(string message)
    {
        var words = message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Join(' ', words);
        if (normalized.Length == 0) return "Cuộc trò chuyện mới";
        if (words.Length <= 8 && normalized.Length <= 40) return normalized;

        var title = string.Join(' ', words.Take(8));
        if (title.Length > 37)
        {
            title = title[..37];
            var boundary = title.LastIndexOf(' ');
            if (boundary > 0) title = title[..boundary];
            else if (char.IsHighSurrogate(title[^1])) title = title[..^1];
        }
        return title.TrimEnd() + "...";
    }

    public static async Task UpdateAsync(IApplicationDbContext context, Conversation conversation,
        string message, CancellationToken cancellationToken)
    {
        var firstMessage = await context.ChatMessages
            .Where(item => item.ConversationId == conversation.Id && item.Role == MessageRole.User)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.Content)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstMessage is null || string.IsNullOrWhiteSpace(conversation.Title) ||
            conversation.Title.Trim() is "Cuộc trò chuyện mới" or "New conversation" or "New chat")
        {
            conversation.Title = BuildTitle(firstMessage ?? message);
        }
    }
}
