using System.Text;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;
using OmniDoc.Domain.Exceptions;
using UglyToad.PdfPig;

namespace OmniDoc.Infrastructure.Services;

public sealed class DocumentFormatDetector : IDocumentFormatDetector
{
    public async Task<DetectedDocumentFormat> DetectAsync(Stream source, string fileName, CancellationToken ct)
    {
        if (!source.CanSeek) throw new InvalidDataException("Không thể đọc lại luồng dữ liệu của tài liệu.");
        var position = source.Position;
        try
        {
            ct.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (extension == ".csv")
            {
                var csv = await new CsvDocumentReader().ReadAsync(source, null, ct);
                return new(DocumentFormat.Csv, csv.ContentType);
            }
            if (extension is ".docm" or ".pptm" or ".xlsm")
                throw new InvalidDataException("Chưa hỗ trợ tệp Office có macro.");
            if (extension is ".docx" or ".pptx" or ".xlsx")
            {
                var signature = new byte[8];
                await source.ReadExactlyAsync(signature, ct);
                source.Position = position;
                if (signature.AsSpan().SequenceEqual(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }))
                    throw new PasswordRequiredException();
                if (!signature.AsSpan(0, 4).SequenceEqual("PK\x03\x04"u8))
                    throw new InvalidDataException("Tài liệu Office phải có cấu trúc OOXML hợp lệ.");
                try { return await OoxmlPreflight.InspectAsync(source, extension, ct); }
                catch (NotSupportedException ex) { throw new InvalidDataException("Không hỗ trợ kiểu nén hoặc mã hóa của tệp Office này.", ex); }
            }
            if (extension == ".pdf")
            {
                var header = new byte[5];
                await source.ReadExactlyAsync(header, ct);
                if (!header.AsSpan().SequenceEqual("%PDF-"u8)) throw new InvalidDataException("Dấu nhận dạng tệp PDF không hợp lệ.");
                source.Position = position;
                try
                {
                    using var pdf = PdfDocument.Open(source);
                    if (pdf.NumberOfPages < 1) throw new InvalidDataException("Tài liệu PDF không có trang nào.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { throw new InvalidDataException("Tài liệu PDF không hợp lệ hoặc chưa được hỗ trợ.", ex); }
                return new(DocumentFormat.Pdf, "application/pdf");
            }
            if (extension is not (".txt" or ".md" or ".markdown"))
                throw new InvalidDataException("Chỉ hỗ trợ tệp PDF, TXT, Markdown, DOCX, PPTX, CSV và XLSX.");
            using var reader = new StreamReader(source, new UTF8Encoding(false, true), false, 4096, leaveOpen: true);
            var buffer = new char[4096];
            var hasText = false;
            var first = true;
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                if (first && new string(buffer, 0, count).TrimStart('\uFEFF').StartsWith("%PDF-", StringComparison.Ordinal))
                    throw new InvalidDataException("Nội dung PDF không khớp với phần mở rộng của tệp.");
                first = false;
                foreach (var c in buffer.AsSpan(0, count))
                {
                    if (char.IsControl(c) && c is not ('\n' or '\r' or '\t' or '\f'))
                        throw new InvalidDataException("Nội dung nhị phân không phải là tài liệu văn bản.");
                    hasText |= !char.IsWhiteSpace(c) && c != '\uFEFF';
                }
            }
            if (!hasText) throw new InvalidDataException("Tài liệu không chứa văn bản.");
            return extension == ".txt" ? new(DocumentFormat.Txt, "text/plain") : new(DocumentFormat.Markdown, "text/markdown");
        }
        catch (DecoderFallbackException ex) { throw new InvalidDataException("Tài liệu văn bản phải sử dụng mã hóa UTF-8.", ex); }
        catch (EndOfStreamException ex) { throw new InvalidDataException("Tài liệu bị thiếu dữ liệu.", ex); }
        finally { source.Position = position; }
    }
}
