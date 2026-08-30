using System.Text;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Features.Retrieval.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Chat.Services;

/// Single source of truth for the RAG prompt so the synchronous and streaming chat paths
/// stay in lockstep — the mock completion provider parses these exact headers back out.
public static class RagPromptBuilder
{
    public const int HistoryMessageLimit = 10;

    public static string BuildSystemPrompt(IReadOnlyList<SearchResultDto> matches)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Bạn là trợ lý AI OmniDoc. Hãy trả lời câu hỏi của người dùng CHỈ dựa trên các tài liệu được cung cấp dưới đây.");
        builder.AppendLine("Nếu không tìm thấy thông tin trong tài liệu, hãy nói rõ rằng bạn không biết.");
        builder.AppendLine("Khi dẫn nguồn, hãy dùng đúng định dạng [Doc: {Tên tài liệu}, Trang {Số trang}].");
        builder.AppendLine("---");
        builder.AppendLine("NGỮ CẢNH TÀI LIỆU:");

        if (matches.Count == 0)
        {
            builder.AppendLine("(Không tìm thấy tài liệu liên quan trong workspace này.)");
            return builder.ToString();
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            builder.AppendLine($"[Nguồn {i + 1}: {match.DocumentTitle} - Trang {match.PageNumber}]");
            builder.AppendLine(match.Content);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static List<ChatPromptMessage> BuildPrompt(
        IReadOnlyList<SearchResultDto> matches,
        IEnumerable<ChatPromptMessage> history,
        string question)
    {
        var prompt = new List<ChatPromptMessage>
        {
            new(nameof(MessageRole.System), BuildSystemPrompt(matches))
        };

        prompt.AddRange(history);
        prompt.Add(new ChatPromptMessage(nameof(MessageRole.User), question));

        return prompt;
    }
}
