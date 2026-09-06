using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Common.Interfaces;

public record DetectedDocumentFormat(DocumentFormat Format, string ContentType);

public interface IDocumentFormatDetector
{
    // The caller owns a seekable stream; detection restores its position.
    Task<DetectedDocumentFormat> DetectAsync(Stream source, string fileName, CancellationToken ct);
}
