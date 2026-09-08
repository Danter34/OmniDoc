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

    public const string EmptyWorkspaceReminder = "Hiện tại không gian làm việc này chưa có tài liệu nào được tải lên. Bạn có thể tải lên tệp tài liệu (PDF, Word, TXT, PowerPoint) để tôi phân tích và trích dẫn bằng chứng cụ thể nhé!";
    public const string NoMatchesReminder = "Hiện tại tôi chưa tìm thấy nội dung tài liệu phù hợp với câu hỏi. Bạn có thể tải thêm tài liệu hoặc bổ sung chi tiết để tôi phân tích và trích dẫn bằng chứng cụ thể nhé!";

    public static string BuildSystemPrompt(IReadOnlyList<SearchResultDto> matches, bool hasDocuments = false)
    {
        var builder = new StringBuilder();

        if (matches.Count == 0)
        {
            builder.AppendLine("Bạn là trợ lý AI OmniDoc. Hãy trò chuyện tự nhiên và trả lời câu hỏi bằng kiến thức tổng quát. Nói rõ khi không chắc chắn.");
            builder.AppendLine("Câu trả lời này không dựa trên tài liệu của workspace. Không bịa nguồn, số trang hoặc tạo ký hiệu trích dẫn tài liệu.");
            builder.AppendLine("Sau câu trả lời, bổ sung lời nhắc thân thiện sau:");
            builder.AppendLine(hasDocuments ? NoMatchesReminder : EmptyWorkspaceReminder);
            return builder.ToString();
        }

        builder.AppendLine("Bạn là trợ lý AI OmniDoc. Hãy trả lời câu hỏi của người dùng CHỈ dựa trên các tài liệu được cung cấp dưới đây.");
        builder.AppendLine("Nếu không tìm thấy thông tin trong tài liệu, hãy nói rõ rằng bạn không biết.");
        builder.AppendLine("Khi dẫn nguồn, hãy dùng đúng định dạng [Doc: {Tên tài liệu}, Trang {Số trang}].");
        builder.AppendLine("---");
        builder.AppendLine("NGỮ CẢNH TÀI LIỆU:");

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
        string question,
        bool hasDocuments = false)
    {
        var prompt = new List<ChatPromptMessage>
        {
            new(nameof(MessageRole.System), BuildSystemPrompt(matches, hasDocuments))
        };

        prompt.AddRange(history);
        prompt.Add(new ChatPromptMessage(nameof(MessageRole.User), question));

        return prompt;
    }
}
