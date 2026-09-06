using System.Text;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;
using UglyToad.PdfPig;

namespace OmniDoc.Infrastructure.Services;

public sealed class DocumentFormatDetector : IDocumentFormatDetector
{
    public async Task<DetectedDocumentFormat> DetectAsync(Stream source, string fileName, CancellationToken ct)
    {
        if (!source.CanSeek) throw new InvalidDataException("A seekable document stream is required.");
        var position = source.Position;
        try
        {
            ct.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (extension == ".pdf")
            {
                var header = new byte[5];
                await source.ReadExactlyAsync(header, ct);
                if (!header.AsSpan().SequenceEqual("%PDF-"u8)) throw new InvalidDataException("Invalid PDF signature.");
                source.Position = position;
                try
                {
                    using var pdf = PdfDocument.Open(source);
                    if (pdf.NumberOfPages < 1) throw new InvalidDataException("PDF has no pages.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { throw new InvalidDataException("Invalid or unsupported PDF document.", ex); }
                return new(DocumentFormat.Pdf, "application/pdf");
            }
            if (extension is not (".txt" or ".md" or ".markdown"))
                throw new InvalidDataException("Only PDF, TXT and Markdown files are supported.");
            using var reader = new StreamReader(source, new UTF8Encoding(false, true), false, 4096, leaveOpen: true);
            var buffer = new char[4096];
            var hasText = false;
            var first = true;
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                if (first && new string(buffer, 0, count).TrimStart('\uFEFF').StartsWith("%PDF-", StringComparison.Ordinal))
                    throw new InvalidDataException("PDF content does not match the file extension.");
                first = false;
                foreach (var c in buffer.AsSpan(0, count))
                {
                    if (char.IsControl(c) && c is not ('\n' or '\r' or '\t' or '\f'))
                        throw new InvalidDataException("Binary content is not a text document.");
                    hasText |= !char.IsWhiteSpace(c) && c != '\uFEFF';
                }
            }
            if (!hasText) throw new InvalidDataException("The document contains no text.");
            return extension == ".txt" ? new(DocumentFormat.Txt, "text/plain") : new(DocumentFormat.Markdown, "text/markdown");
        }
        catch (DecoderFallbackException ex) { throw new InvalidDataException("Text documents must use UTF-8 encoding.", ex); }
        catch (EndOfStreamException ex) { throw new InvalidDataException("The document is truncated.", ex); }
        finally { source.Position = position; }
    }
}
