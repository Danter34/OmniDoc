namespace OmniDoc.Application.Common.Interfaces;

public record PdfPageContent(int PageNumber, string Text);

public interface IPdfParserService
{
    Task<IReadOnlyList<PdfPageContent>> ExtractPagesAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}
