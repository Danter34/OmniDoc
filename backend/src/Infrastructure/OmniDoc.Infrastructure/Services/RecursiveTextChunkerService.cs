using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.Infrastructure.Services;

public class RecursiveTextChunkerService : ITextChunkerService
{
    private static readonly string[] Separators = ["\n\n", "\n", ". ", " "];

    public IReadOnlyList<TextChunkItem> ChunkPages(IReadOnlyList<PdfPageContent> pages, int maxChunkSize = 800, int chunkOverlap = 150)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChunkSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkOverlap);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(chunkOverlap, maxChunkSize);

        var chunks = new List<TextChunkItem>();
        var chunkIndex = 0;

        foreach (var page in pages)
        {
            foreach (var content in SplitText(page.Text, maxChunkSize, chunkOverlap))
            {
                chunks.Add(new TextChunkItem(chunkIndex++, page.PageNumber, content));
            }
        }

        return chunks;
    }

    private static List<string> SplitText(string text, int maxChunkSize, int chunkOverlap)
    {
        var results = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return results;
        }

        var trimmed = text.Trim();
        var position = 0;

        while (position < trimmed.Length)
        {
            var remaining = trimmed.Length - position;

            if (remaining <= maxChunkSize)
            {
                AppendIfNotEmpty(results, trimmed[position..]);
                break;
            }

            var windowLength = FindBreakPoint(trimmed, position, maxChunkSize);
            AppendIfNotEmpty(results, trimmed.Substring(position, windowLength));

            var advance = Math.Max(1, windowLength - chunkOverlap);
            position = SnapToWordStart(trimmed, position + advance, position + 1);
        }

        return results;
    }

    // The overlap offset usually lands mid-word; nudge it to the nearest word start
    // so a chunk never opens with a truncated token.
    private static int SnapToWordStart(string text, int candidate, int minPosition)
    {
        if (candidate >= text.Length)
        {
            return candidate;
        }

        var snapped = candidate;
        while (snapped > minPosition && !char.IsWhiteSpace(text[snapped - 1]))
        {
            snapped--;
        }

        if (snapped > minPosition)
        {
            return snapped;
        }

        snapped = candidate;
        while (snapped < text.Length && !char.IsWhiteSpace(text[snapped]))
        {
            snapped++;
        }

        while (snapped < text.Length && char.IsWhiteSpace(text[snapped]))
        {
            snapped++;
        }

        return Math.Max(snapped, minPosition);
    }

    // Prefers the largest semantic boundary that still fits the window so chunks
    // do not end mid-sentence or mid-word.
    private static int FindBreakPoint(string text, int start, int maxChunkSize)
    {
        var minLength = maxChunkSize / 2;

        foreach (var separator in Separators)
        {
            var searchEnd = start + maxChunkSize;
            var index = text.LastIndexOf(separator, searchEnd - 1, maxChunkSize, StringComparison.Ordinal);

            if (index > start)
            {
                var length = index - start + separator.Length;
                if (length >= minLength)
                {
                    return length;
                }
            }
        }

        return maxChunkSize;
    }

    private static void AppendIfNotEmpty(List<string> results, string candidate)
    {
        var value = candidate.Trim();
        if (value.Length > 0)
        {
            results.Add(value);
        }
    }
}
