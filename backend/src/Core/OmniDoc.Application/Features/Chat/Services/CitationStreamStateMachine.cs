using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using OmniDoc.Application.Features.Chat.DTOs;
using OmniDoc.Application.Features.Retrieval.DTOs;

namespace OmniDoc.Application.Features.Chat.Services;

/// Turns a raw LLM token stream into client-safe <see cref="ChatStreamEvent"/> frames.
///
/// The provider splits its output at arbitrary offsets, so a citation tag such as
/// "[Doc: Report.pdf, Trang 2]" routinely arrives as three unrelated chunks. Forwarding
/// those verbatim makes the UI flicker with half-written markup. This machine therefore
/// runs a two-state scanner over the stream: text flows straight through, and everything
/// from an opening '[' is held in a buffer until it either resolves to a real retrieved
/// chunk (emitted as a citation event, markup stripped from the prose) or turns out not
/// to be a citation at all (flushed back into the prose verbatim).
///
/// The invariant that matters: no character the provider produced is ever dropped unless
/// it belonged to a citation tag that was successfully resolved.
public partial class CitationStreamStateMachine
{
    /// A candidate longer than this is treated as prose that merely happens to contain '['.
    /// Bounds how long the UI can stall waiting for a closing bracket that never comes.
    private const int MaxBufferLength = 150;

    private const int ExcerptLength = 400;

    private static readonly string[] CitationKeywords =
        ["Doc", "Document", "Nguồn", "Nguon", "Source", "Tài liệu", "Tai lieu"];

    private static readonly string[] TitleExtensions =
        [".pdf", ".docx", ".doc", ".txt", ".md", ".pptx", ".xlsx"];

    private enum State
    {
        DirectText,
        Buffering
    }

    public async IAsyncEnumerable<ChatStreamEvent> ProcessStreamAsync(
        IAsyncEnumerable<string> tokenStream,
        IReadOnlyList<SearchResultDto> contextChunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenStream);
        ArgumentNullException.ThrowIfNull(contextChunks);

        var state = State.DirectText;
        var pending = new StringBuilder();
        var buffer = new StringBuilder();

        // Set after a "[[...]" citation resolves on its first ']' so the redundant second
        // ']' is swallowed instead of leaking into the prose as a stray character.
        var skipNextCloseBracket = false;

        await foreach (var chunk in tokenStream.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            foreach (var character in chunk)
            {
                if (skipNextCloseBracket)
                {
                    skipNextCloseBracket = false;

                    if (character == ']')
                    {
                        continue;
                    }
                }

                if (state == State.DirectText)
                {
                    if (character == '[')
                    {
                        if (pending.Length > 0)
                        {
                            yield return new ChatStreamEvent(StreamEventType.Token, pending.ToString());
                            pending.Clear();
                        }

                        state = State.Buffering;
                        buffer.Append(character);
                    }
                    else
                    {
                        pending.Append(character);
                    }

                    continue;
                }

                // State.Buffering: every branch below either keeps accumulating or moves the
                // buffered characters into `pending`, never discards them silently.
                if (character is '\n' or '\r')
                {
                    // Citation tags never span lines, so this candidate is prose.
                    buffer.Append(character);
                    FlushBufferInto(pending, buffer);
                    state = State.DirectText;
                    continue;
                }

                buffer.Append(character);

                if (character == ']')
                {
                    var citation = TryResolveCitation(buffer.ToString(), contextChunks);

                    if (citation is not null)
                    {
                        skipNextCloseBracket = CountLeading(buffer, '[') > CountTrailing(buffer, ']');
                        buffer.Clear();
                        state = State.DirectText;

                        yield return new ChatStreamEvent(StreamEventType.Citation, Citation: citation);
                        continue;
                    }

                    FlushBufferInto(pending, buffer);
                    state = State.DirectText;
                    continue;
                }

                if (buffer.Length >= MaxBufferLength || !CouldStillBeCitation(buffer))
                {
                    FlushBufferInto(pending, buffer);
                    state = State.DirectText;
                }
            }

            if (pending.Length > 0)
            {
                yield return new ChatStreamEvent(StreamEventType.Token, pending.ToString());
                pending.Clear();
            }
        }

        // The stream ended mid-candidate (provider finished or the client disconnected):
        // emit the partial markup as plain text rather than losing the words.
        if (buffer.Length > 0)
        {
            FlushBufferInto(pending, buffer);
        }

        if (pending.Length > 0)
        {
            yield return new ChatStreamEvent(StreamEventType.Token, pending.ToString());
        }
    }

    private static void FlushBufferInto(StringBuilder pending, StringBuilder buffer)
    {
        pending.Append(buffer);
        buffer.Clear();
    }

    /// Rejects a candidate as soon as it cannot become a citation, so "[1, 2, 3]" starts
    /// flowing to the client on its second character instead of stalling the stream.
    private static bool CouldStillBeCitation(StringBuilder buffer)
    {
        var candidate = buffer.ToString();
        var index = 0;

        while (index < candidate.Length && candidate[index] == '[')
        {
            index++;
        }

        if (index > 2)
        {
            return false;
        }

        while (index < candidate.Length && char.IsWhiteSpace(candidate[index]))
        {
            index++;
        }

        var rest = candidate[index..];

        if (rest.Length == 0)
        {
            return true;
        }

        var separatorIndex = rest.IndexOfAny([':', '-']);

        // Past the separator the remainder is a free-form title, so any content is allowed.
        if (separatorIndex < 0)
        {
            return CitationKeywords.Any(keyword =>
                keyword.StartsWith(rest, StringComparison.OrdinalIgnoreCase));
        }

        var keywordPart = rest[..separatorIndex].TrimEnd();

        return CitationKeywords.Any(keyword =>
            keyword.Equals(keywordPart, StringComparison.OrdinalIgnoreCase));
    }

    /// Parses the buffered markup and binds it to a chunk that was actually retrieved.
    /// Returns null for anything unparseable or unattributable — a hallucinated source
    /// must not become a citation the user can click.
    private static CitationDto? TryResolveCitation(string candidate, IReadOnlyList<SearchResultDto> contextChunks)
    {
        var match = CitationTagRegex().Match(candidate);

        if (!match.Success)
        {
            return null;
        }

        var title = match.Groups["title"].Value.Trim();
        var pageGroup = match.Groups["page"];
        int? page = pageGroup.Success && int.TryParse(pageGroup.Value, out var parsedPage) ? parsedPage : null;

        var chunk = ResolveChunk(title, page, contextChunks);

        if (chunk is null)
        {
            return null;
        }

        return new CitationDto(
            chunk.ChunkId,
            chunk.DocumentId,
            chunk.DocumentTitle,
            chunk.PageNumber,
            Truncate(chunk.Content, ExcerptLength),
            chunk.SimilarityScore);
    }

    private static SearchResultDto? ResolveChunk(
        string title,
        int? page,
        IReadOnlyList<SearchResultDto> contextChunks)
    {
        if (contextChunks.Count == 0)
        {
            return null;
        }

        var normalizedTitle = NormalizeTitle(title);

        if (normalizedTitle.Length == 0)
        {
            return null;
        }

        if (page is { } pageNumber)
        {
            var exact = contextChunks.FirstOrDefault(chunk =>
                chunk.PageNumber == pageNumber && NormalizeTitle(chunk.DocumentTitle) == normalizedTitle);

            if (exact is not null)
            {
                return exact;
            }
        }

        var byTitle = contextChunks.FirstOrDefault(chunk =>
            NormalizeTitle(chunk.DocumentTitle) == normalizedTitle);

        if (byTitle is not null)
        {
            return byTitle;
        }

        // The model often shortens or expands the file name; fall back to containment,
        // preferring a candidate whose page also agrees.
        var fuzzy = contextChunks.Where(chunk => IsFuzzyTitleMatch(chunk.DocumentTitle, normalizedTitle)).ToList();

        if (fuzzy.Count == 0)
        {
            return null;
        }

        return page is { } wantedPage
            ? fuzzy.FirstOrDefault(chunk => chunk.PageNumber == wantedPage) ?? fuzzy[0]
            : fuzzy[0];
    }

    private static bool IsFuzzyTitleMatch(string documentTitle, string normalizedTitle)
    {
        var normalizedDocumentTitle = NormalizeTitle(documentTitle);

        if (normalizedDocumentTitle.Length == 0)
        {
            return false;
        }

        return normalizedDocumentTitle.Contains(normalizedTitle, StringComparison.Ordinal)
            || normalizedTitle.Contains(normalizedDocumentTitle, StringComparison.Ordinal);
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = WhitespaceRegex().Replace(title.Trim(), " ").ToLowerInvariant();

        foreach (var extension in TitleExtensions)
        {
            if (normalized.EndsWith(extension, StringComparison.Ordinal))
            {
                normalized = normalized[..^extension.Length];
                break;
            }
        }

        return normalized.Trim();
    }

    private static int CountLeading(StringBuilder buffer, char character)
    {
        var count = 0;

        while (count < buffer.Length && buffer[count] == character)
        {
            count++;
        }

        return count;
    }

    private static int CountTrailing(StringBuilder buffer, char character)
    {
        var count = 0;

        while (count < buffer.Length && buffer[buffer.Length - 1 - count] == character)
        {
            count++;
        }

        return count;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "...";

    [GeneratedRegex(
        @"^\[{1,2}\s*(?:Document|Doc|Nguồn|Nguon|Source|Tài liệu|Tai lieu)\s*[:\-]\s*(?<title>[^\]]*?)\s*(?:[,;|]\s*(?:Trang|Page|Tr\.?|p\.?)\s*(?<page>\d+)\s*)?\]{1,2}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex CitationTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
