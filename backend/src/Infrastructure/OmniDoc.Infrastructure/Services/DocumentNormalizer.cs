using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Infrastructure.Services;

public sealed class DocumentNormalizer(PassThroughPdfNormalizer pdf, GotenbergChromiumNormalizer chromium, GotenbergLibreOfficeNormalizer office) : IDocumentNormalizer
{
    public Task<CanonicalPdfResult> NormalizeAsync(Stream sourceStream, DocumentFormat format, CancellationToken ct) =>
        format switch
        {
            DocumentFormat.Pdf => pdf.NormalizeAsync(sourceStream, format, ct),
            DocumentFormat.Txt or DocumentFormat.Markdown => chromium.NormalizeAsync(sourceStream, format, ct),
            DocumentFormat.Docx or DocumentFormat.Pptx => office.NormalizeAsync(sourceStream, format, ct),
            _ => throw new NotSupportedException("Unsupported document format.")
        };
}
