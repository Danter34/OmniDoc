using System.Text.RegularExpressions;
using OmniDoc.Application.Common.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace OmniDoc.Infrastructure.Services;

public partial class PdfPigParserService : IPdfParserService
{
    public Task<IReadOnlyList<PdfPageContent>> ExtractPagesAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pages = new List<PdfPageContent>();

        using var document = PdfDocument.Open(pdfStream);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = ContentOrderTextExtractor.GetText(page) ?? string.Empty;
            pages.Add(new PdfPageContent(page.Number, NormalizeWhitespace(text)));
        }

        return Task.FromResult<IReadOnlyList<PdfPageContent>>(pages);
    }

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var collapsed = HorizontalWhitespaceRegex().Replace(text, " ");
        collapsed = SpaceAroundNewlineRegex().Replace(collapsed, "\n");
        collapsed = ExcessNewlineRegex().Replace(collapsed, "\n\n");

        return collapsed.Trim();
    }

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex HorizontalWhitespaceRegex();

    [GeneratedRegex(@" ?\n ?")]
    private static partial Regex SpaceAroundNewlineRegex();

    [GeneratedRegex(@"\n\s*\n\s*(\n\s*)+")]
    private static partial Regex ExcessNewlineRegex();
}
