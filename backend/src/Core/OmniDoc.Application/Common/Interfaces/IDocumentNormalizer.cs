using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Common.Interfaces;

// The caller owns and disposes the returned PDF stream.
public record CanonicalPdfResult(Stream Content, string Producer);

public interface IDocumentNormalizer
{
    Task<CanonicalPdfResult> NormalizeAsync(Stream sourceStream, DocumentFormat format, CancellationToken ct);
}
