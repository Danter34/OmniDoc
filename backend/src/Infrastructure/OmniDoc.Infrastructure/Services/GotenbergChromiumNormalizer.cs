using System.Net;
using System.Text;
using Markdig;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Infrastructure.Services;

public sealed class GotenbergChromiumNormalizer(HttpClient client, IDocumentFormatDetector detector) : IDocumentNormalizer
{
    private static readonly MarkdownPipeline Markdown = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    public async Task<CanonicalPdfResult> NormalizeAsync(Stream sourceStream, DocumentFormat format, CancellationToken ct)
    {
        if (format is not (DocumentFormat.Txt or DocumentFormat.Markdown)) throw new NotSupportedException("Unsupported conversion format.");
        await detector.DetectAsync(sourceStream, format == DocumentFormat.Txt ? "source.txt" : "source.md", ct);
        using var reader = new StreamReader(sourceStream, new UTF8Encoding(false, true), true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        var body = format == DocumentFormat.Txt ? $"<pre>{WebUtility.HtmlEncode(text)}</pre>" : Markdig.Markdown.ToHtml(text, Markdown);
        // CSP prevents uploaded content from executing scripts or fetching external/local resources.
        var html = """
            <!doctype html><html><head><meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src 'none'; script-src 'none'; base-uri 'none'">
            <style>
            @page { size: A4; margin: 20mm; }
            body { font-family: Arial, sans-serif; font-size: 11pt; line-height: 1.5; overflow-wrap: anywhere; color: #111; }
            pre { white-space: pre-wrap; font-family: inherit; }
            h1,h2,h3,h4 { break-after: avoid; } p { orphans: 3; widows: 3; }
            table { border-collapse: collapse; width: 100%; } th,td { border: 1px solid #bbb; padding: 6px; }
            tr, img { break-inside: avoid; } code { font-family: monospace; }
            </style></head><body>
            """ + body + "</body></html>";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(html, Encoding.UTF8, "text/html"), "files", "index.html");
        form.Add(new StringContent("true"), "preferCssPageSize");
        form.Add(new StringContent("0"), "marginTop");
        form.Add(new StringContent("0"), "marginBottom");
        form.Add(new StringContent("0"), "marginLeft");
        form.Add(new StringContent("0"), "marginRight");
        using var response = await ConvertAsync(form, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType != "application/pdf")
            throw new InvalidDataException("Converter did not return a PDF.");
        var pdf = new MemoryStream(await response.Content.ReadAsByteArrayAsync(ct));
        try
        {
            await detector.DetectAsync(pdf, "canonical.pdf", ct);
            return new(pdf, "GotenbergChromium");
        }
        catch { await pdf.DisposeAsync(); throw; }
    }

    private async Task<HttpResponseMessage> ConvertAsync(HttpContent form, CancellationToken ct)
    {
        try { return await client.PostAsync("forms/chromium/convert/html", form, ct); }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        { throw new TimeoutException("Document conversion timed out.", ex); }
    }
}
