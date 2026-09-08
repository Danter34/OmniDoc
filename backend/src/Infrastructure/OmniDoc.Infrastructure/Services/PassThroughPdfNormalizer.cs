using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Infrastructure.Services;

public sealed class PassThroughPdfNormalizer(IDocumentFormatDetector detector) : IDocumentNormalizer
{
    public async Task<CanonicalPdfResult> NormalizeAsync(Stream sourceStream, DocumentFormat format, CancellationToken ct)
    {
        if (format != DocumentFormat.Pdf) throw new NotSupportedException("Expected PDF.");
        await detector.DetectAsync(sourceStream, "document.pdf", ct);
        var copy = new MemoryStream();
        try
        {
            await sourceStream.CopyToAsync(copy, ct);
            copy.Position = 0;
            return new(copy, "PassThrough");
        }
        catch { await copy.DisposeAsync(); throw; }
    }
}
