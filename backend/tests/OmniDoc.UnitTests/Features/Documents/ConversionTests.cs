using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Moq;
using Moq.Protected;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Services;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace OmniDoc.UnitTests.Features.Documents;

internal static class PdfFixture
{
    public static byte[] Create()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText("Canonical evidence", 12, new PdfPoint(50, 750), font);
        return builder.Build();
    }
}

public sealed class ConversionTests
{
    [Theory]
    [InlineData("note.txt", DocumentFormat.Txt, "text/plain")]
    [InlineData("note.md", DocumentFormat.Markdown, "text/markdown")]
    [InlineData("note.MARKDOWN", DocumentFormat.Markdown, "text/markdown")]
    public async Task Detector_AcceptsUtf8AndRestoresPosition(string name, DocumentFormat format, string mime)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\uFEFF# Tiếng Việt\nNội dung\tđối soát"));
        var detected = await new DocumentFormatDetector().DetectAsync(stream, name, default);
        Assert.Equal(format, detected.Format);
        Assert.Equal(mime, detected.ContentType);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task Detector_AcceptsRealPdf()
    {
        using var stream = new MemoryStream(PdfFixture.Create());
        var detected = await new DocumentFormatDetector().DetectAsync(stream, "real.PDF", default);
        Assert.Equal(DocumentFormat.Pdf, detected.Format);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData("fake.pdf", "not PDF")]
    [InlineData("fake.pdf", "%PDF-1.7\nno valid objects")]
    [InlineData("fake.txt", "%PDF-1.7\n")]
    [InlineData("fake.md", "PK\u0003\u0004binary")]
    [InlineData("fake.txt", "text\u0000binary")]
    [InlineData("empty.txt", " \n\t")]
    [InlineData("office.docx", "hello")]
    public async Task Detector_RejectsInvalidContent(string name, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentFormatDetector().DetectAsync(stream, name, default));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task Detector_RejectsInvalidUtf8AfterFirstBuffer()
    {
        using var stream = new MemoryStream(Enumerable.Repeat((byte)'a', 5000).Concat(new byte[] { 0xff, 0xfe }).ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentFormatDetector().DetectAsync(stream, "note.md", default));
    }

    [Theory]
    [InlineData(DocumentFormat.Txt, "<script>alert(1)</script>\nline two", "&lt;script&gt;")]
    [InlineData(DocumentFormat.Markdown, "# Heading\n\n**Evidence**\n\n<script>alert(1)</script>", "<strong>Evidence</strong>")]
    public async Task Chromium_PostsSafeHtmlAndReturnsValidatedPdf(DocumentFormat format, string text, string expectedHtml)
    {
        string? html = null;
        var expectedPdf = PdfFixture.Create();
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/forms/chromium/convert/html", request.RequestUri!.AbsolutePath);
                var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
                var file = multipart.Single(c => c.Headers.ContentDisposition?.FileName?.Trim('"') == "index.html");
                html = await file.ReadAsStringAsync(ct);
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(expectedPdf) };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                return response;
            });
        using var client = new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("http://converter/") };
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var result = await new GotenbergChromiumNormalizer(client, new DocumentFormatDetector()).NormalizeAsync(source, format, default);
        await using var pdf = result.Content;
        Assert.Equal("GotenbergChromium", result.Producer);
        Assert.Contains(expectedHtml, html);
        Assert.Contains("default-src 'none'", html);
        Assert.Contains("size: A4", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Equal(expectedPdf, ((MemoryStream)pdf).ToArray());
    }

    [Theory]
    [InlineData(503, "application/pdf", false)]
    [InlineData(200, "text/html", false)]
    [InlineData(200, "application/pdf", true)]
    public async Task Chromium_RejectsErrorsAndNonPdfResponses(int status, string mime, bool corrupt)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new ByteArrayContent(corrupt ? "%PDF-broken"u8.ToArray() : PdfFixture.Create()) };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(mime);
                return response;
            });
        using var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://converter/") };
        using var stream = new MemoryStream("hello"u8.ToArray());
        var normalizer = new GotenbergChromiumNormalizer(client, new DocumentFormatDetector());
        if (status != 200) await Assert.ThrowsAsync<HttpRequestException>(() => normalizer.NormalizeAsync(stream, DocumentFormat.Txt, default));
        else await Assert.ThrowsAsync<InvalidDataException>(() => normalizer.NormalizeAsync(stream, DocumentFormat.Txt, default));
    }

    [Fact]
    public async Task Chromium_PropagatesCancellation()
    {
        using var client = new HttpClient(new Mock<HttpMessageHandler>(MockBehavior.Strict).Object, disposeHandler: false);
        using var source = new MemoryStream("hello"u8.ToArray());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GotenbergChromiumNormalizer(client, new DocumentFormatDetector())
            .NormalizeAsync(source, DocumentFormat.Txt, new CancellationToken(true)));
    }

    [Fact]
    public async Task PassThrough_PreservesPdfBytesAndSourceOwnership()
    {
        var bytes = PdfFixture.Create();
        using var source = new MemoryStream(bytes);
        var result = await new PassThroughPdfNormalizer(new DocumentFormatDetector()).NormalizeAsync(source, DocumentFormat.Pdf, default);
        await using var output = result.Content;
        Assert.Equal(bytes, ((MemoryStream)output).ToArray());
        Assert.Equal("PassThrough", result.Producer);
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task Chromium_ReportsTimeoutAsConversionFailure()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("HTTP timeout"));
        using var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://converter/") };
        using var source = new MemoryStream("hello"u8.ToArray());
        await Assert.ThrowsAsync<TimeoutException>(() => new GotenbergChromiumNormalizer(client, new DocumentFormatDetector())
            .NormalizeAsync(source, DocumentFormat.Txt, default));
    }
}
