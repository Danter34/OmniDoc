using System.Runtime.CompilerServices;
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
    private const int MinWordsPerChunk = 1;
    private const int MaxWordsPerChunk = 3;
    private const int MinDelayMs = 30;
    private const int MaxDelayMs = 50;

    /// Fixed seed: the split points must be reproducible so a failing stream test can be
    /// replayed, while still being irregular enough to exercise the state machine.
    private const int SplitSeed = 20260830;

    public Task<string> GenerateResponseAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(BuildAnswer(messages));
    }

    /// Replays the same answer the synchronous path would return, but chopped at arbitrary
    /// offsets — including inside "[Doc: ..., Trang N]" tags — so the citation state machine
    /// is exercised against the token-splitting it has to survive in production.
    public async IAsyncEnumerable<string> StreamResponseAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var answer = BuildAnswer(messages);
        var random = new Random(SplitSeed);

        foreach (var chunk in SplitIntoChunks(answer, random))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(random.Next(MinDelayMs, MaxDelayMs + 1), cancellationToken).ConfigureAwait(false);

            yield return chunk;
        }
    }

    private static string BuildAnswer(IReadOnlyList<ChatPromptMessage> messages)
    {
        var systemPrompt = messages
            .LastOrDefault(m => string.Equals(m.Role, "System", StringComparison.OrdinalIgnoreCase))
            ?.Content ?? string.Empty;

        var question = messages
            .LastOrDefault(m => string.Equals(m.Role, "User", StringComparison.OrdinalIgnoreCase))
            ?.Content ?? string.Empty;

        var sources = ParseSources(systemPrompt);

        return sources.Count == 0
            ? BuildNoContextAnswer(question)
            : BuildGroundedAnswer(question, sources);
    }

    /// Emits 1-3 words at a time, then splits roughly every other chunk again at a random
    /// character offset. The second pass is what produces the interesting cases: citation
    /// tags arriving as "[Doc: " / "BaoCao.pdf, " / "Trang 1]".
    private static IEnumerable<string> SplitIntoChunks(string answer, Random random)
    {
        if (answer.Length == 0)
        {
            yield break;
        }

        foreach (var group in GroupWords(answer, random))
        {
            if (group.Length > 3 && random.Next(2) == 0)
            {
                var cut = random.Next(1, group.Length);

                yield return group[..cut];
                yield return group[cut..];
            }
            else
            {
                yield return group;
            }
        }
    }

    /// Splits on whitespace while keeping every separator attached to the preceding word, so
    /// concatenating the chunks reproduces the answer byte for byte.
    private static IEnumerable<string> GroupWords(string answer, Random random)
    {
        var builder = new StringBuilder();
        var wordsInGroup = 0;
        var target = random.Next(MinWordsPerChunk, MaxWordsPerChunk + 1);
        var index = 0;

        while (index < answer.Length)
        {
            var wordStart = index;

            while (index < answer.Length && !char.IsWhiteSpace(answer[index]))
            {
                index++;
            }

            while (index < answer.Length && char.IsWhiteSpace(answer[index]))
            {
                index++;
            }

            builder.Append(answer[wordStart..index]);
            wordsInGroup++;

            if (wordsInGroup < target)
            {
                continue;
            }

            yield return builder.ToString();
            builder.Clear();
            wordsInGroup = 0;
            target = random.Next(MinWordsPerChunk, MaxWordsPerChunk + 1);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
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
