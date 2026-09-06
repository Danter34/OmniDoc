using System.Net;
using System.Net.Http.Headers;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;
using OmniDoc.Domain.Exceptions;

namespace OmniDoc.Infrastructure.Services;

public sealed class GotenbergLibreOfficeNormalizer(HttpClient client, IDocumentFormatDetector detector) : IDocumentNormalizer
{
    public async Task<CanonicalPdfResult> NormalizeAsync(Stream sourceStream, DocumentFormat format, CancellationToken ct)
    {
        if (format is not (DocumentFormat.Docx or DocumentFormat.Pptx or DocumentFormat.Xlsx)) throw new NotSupportedException("Expected DOCX, PPTX or XLSX.");
        var fileName = format switch { DocumentFormat.Docx => "source.docx", DocumentFormat.Pptx => "source.pptx", _ => "source.xlsx" };
        var detected = await detector.DetectAsync(sourceStream, fileName, ct);
        // Own the multipart copy; disposing the request must not dispose the caller's stream.
        using var upload = new MemoryStream();
        await sourceStream.CopyToAsync(upload, ct);
        upload.Position = 0;
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(upload);
        file.Headers.ContentType = new MediaTypeHeaderValue(detected.ContentType);
        form.Add(file, "files", fileName);
        form.Add(new StringContent("false"), "updateIndexes");
        form.Add(new StringContent("false"), "exportNotes");
        form.Add(new StringContent("false"), "exportNotesPages");
        form.Add(new StringContent("false"), "exportHiddenSlides");
        if (format == DocumentFormat.Xlsx)
            form.Add(new StringContent("false"), "singlePageSheets");
        // Do not set paper dimensions/orientation: Impress preserves the slide canvas.
        try
        {
            using var response = await client.PostAsync("forms/libreoffice/convert", form, ct);
            if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout)
                throw new DocumentProcessingException(DocumentFailureCode.ConversionTimeout, "Office conversion timed out.");
            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new DocumentProcessingException(DocumentFailureCode.ConverterUnavailable, "Office converter is unavailable. Please retry later.");
            if (!response.IsSuccessStatusCode)
                throw new DocumentProcessingException(DocumentFailureCode.ConversionFailed, "LibreOffice could not convert this Office document.");
            if (response.Content.Headers.ContentType?.MediaType != "application/pdf")
                throw new DocumentProcessingException(DocumentFailureCode.ConversionFailed, "Office converter returned an invalid PDF response.");
            var pdf = new MemoryStream(await response.Content.ReadAsByteArrayAsync(ct));
            try
            {
                await detector.DetectAsync(pdf, "canonical.pdf", ct);
                return new(pdf, "GotenbergLibreOffice");
            }
            catch { await pdf.DisposeAsync(); throw; }
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        { throw new DocumentProcessingException(DocumentFailureCode.ConversionTimeout, "Office conversion timed out.", ex); }
        catch (HttpRequestException ex)
        { throw new DocumentProcessingException(DocumentFailureCode.ConverterUnavailable, "Office converter is unavailable. Please retry later.", ex); }
        catch (InvalidDataException ex)
        { throw new DocumentProcessingException(DocumentFailureCode.ConversionFailed, "Office converter returned an invalid PDF.", ex); }
    }
}
