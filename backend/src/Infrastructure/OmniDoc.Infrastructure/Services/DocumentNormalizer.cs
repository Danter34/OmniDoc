using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Infrastructure.Services;

public sealed class DocumentNormalizer(PassThroughPdfNormalizer pdf, GotenbergChromiumNormalizer chromium) : IDocumentNormalizer
{
    public Task<CanonicalPdfResult> NormalizeAsync(Stream sourceStream, DocumentFormat format, CancellationToken ct) =>
        format == DocumentFormat.Pdf ? pdf.NormalizeAsync(sourceStream, format, ct) : chromium.NormalizeAsync(sourceStream, format, ct);
}
