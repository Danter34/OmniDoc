using System.Text;
using System.Text.RegularExpressions;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.Infrastructure.Services;

/// Placeholder completion provider: synthesises an answer from the context block that
/// SendMessageCommandHandler puts in the system prompt. Swap for an IChatClient
/// (Microsoft.Extensions.AI) backed by Ollama/OpenAI/Gemini once configured.
public partial class MockChatCompletionService : IChatCompletionService
{
    private const int SentencesPerSource = 2;

    public Task<string> GenerateResponseAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var systemPrompt = messages
            .LastOrDefault(m => string.Equals(m.Role, "System", StringComparison.OrdinalIgnoreCase))
            ?.Content ?? string.Empty;

        var question = messages
            .LastOrDefault(m => string.Equals(m.Role, "User", StringComparison.OrdinalIgnoreCase))
            ?.Content ?? string.Empty;

        var sources = ParseSources(systemPrompt);

        return Task.FromResult(sources.Count == 0
            ? BuildNoContextAnswer(question)
            : BuildGroundedAnswer(question, sources));
    }

    private static string BuildNoContextAnswer(string question)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Tôi không tìm thấy thông tin liên quan trong các tài liệu của workspace này, nên tôi không thể trả lời chắc chắn.");
        builder.AppendLine();
        builder.Append("Bạn hãy thử tải lên tài liệu có nội dung về \"");
        builder.Append(Shorten(question, 80));
        builder.AppendLine("\" hoặc diễn đạt lại câu hỏi cụ thể hơn.");

        return builder.ToString().TrimEnd();
    }

    private static string BuildGroundedAnswer(string question, List<PromptSource> sources)
    {
        var builder = new StringBuilder();

        builder.Append("Dựa trên tài liệu trong workspace, đây là nội dung liên quan đến câu hỏi \"");
        builder.Append(Shorten(question, 120));
        builder.AppendLine("\":");
        builder.AppendLine();

        foreach (var source in sources)
        {
            builder.Append("- ");
            builder.Append(SummariseContent(source.Content));
            builder.Append(' ');
            builder.AppendLine($"[Doc: {source.DocumentTitle}, Trang {source.PageNumber}]");
        }

        builder.AppendLine();
        builder.Append("Các trích dẫn ở trên là toàn bộ căn cứ tôi có; nếu bạn cần chi tiết nằm ngoài phần này, tôi chưa có thông tin trong tài liệu.");

        return builder.ToString();
    }

    private static string SummariseContent(string content)
    {
        var normalized = WhitespaceRegex().Replace(content, " ").Trim();

        if (normalized.Length == 0)
        {
            return "(đoạn văn bản trống)";
        }

        var sentences = normalized
            .Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(sentence => sentence.Length > 0)
            .Take(SentencesPerSource)
            .ToList();

        var summary = sentences.Count > 0 ? string.Join(". ", sentences) + "." : normalized;

        return Shorten(summary, 320);
    }

    private static List<PromptSource> ParseSources(string systemPrompt)
    {
        var sources = new List<PromptSource>();
        var matches = SourceHeaderRegex().Matches(systemPrompt);

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var contentStart = match.Index + match.Length;
            var contentEnd = i + 1 < matches.Count ? matches[i + 1].Index : systemPrompt.Length;

            sources.Add(new PromptSource(
                match.Groups["title"].Value.Trim(),
                int.Parse(match.Groups["page"].Value),
                systemPrompt[contentStart..contentEnd].Trim()));
        }

        return sources;
    }

    [GeneratedRegex(@"\[Nguồn \d+: (?<title>.+?) - Trang (?<page>\d+)\]", RegexOptions.Compiled)]
    private static partial Regex SourceHeaderRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    private static string Shorten(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "...";

    private sealed record PromptSource(string DocumentTitle, int PageNumber, string Content);
}
