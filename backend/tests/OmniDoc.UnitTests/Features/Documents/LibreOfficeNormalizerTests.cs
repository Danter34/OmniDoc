using System.Net;
using System.Net.Http.Headers;
using Moq;
using Moq.Protected;
using OmniDoc.Domain.Enums;
using OmniDoc.Domain.Exceptions;
using OmniDoc.Infrastructure.Services;

namespace OmniDoc.UnitTests.Features.Documents;

public sealed class LibreOfficeNormalizerTests
{
    [Theory]
    [InlineData(DocumentFormat.Docx, "source.docx", OfficeFixture.DocxMime)]
    [InlineData(DocumentFormat.Pptx, "source.pptx", OfficeFixture.PptxMime)]
    public async Task RouterSendsOfficeBytesWithLayoutOptions(DocumentFormat format, string name, string mime)
    {
        var bytes = OfficeFixture.Create(format);
        var pdfBytes = PdfFixture.Create();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
            {
                Assert.Equal("/forms/libreoffice/convert", request.RequestUri!.AbsolutePath);
                Assert.Equal(HttpMethod.Post, request.Method);
                var form = Assert.IsType<MultipartFormDataContent>(request.Content);
                var file = form.Single(f => f.Headers.ContentDisposition?.Name?.Trim('"') == "files");
                Assert.Equal(name, file.Headers.ContentDisposition!.FileName!.Trim('"'));
                Assert.Equal(mime, file.Headers.ContentType!.MediaType);
                Assert.Equal(bytes, await file.ReadAsByteArrayAsync(ct));
                foreach (var field in new[] { "updateIndexes", "exportNotes", "exportNotesPages", "exportHiddenSlides" })
                    Assert.Equal("false", await form.Single(f => f.Headers.ContentDisposition?.Name?.Trim('"') == field).ReadAsStringAsync(ct));
                Assert.DoesNotContain(form, f => f.Headers.ContentDisposition?.Name?.Trim('"') is "landscape" or "paperWidth" or "paperHeight");
                return Response(HttpStatusCode.OK, pdfBytes);
            });
        using var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://converter/") };
        var detector = new DocumentFormatDetector();
        var router = new DocumentNormalizer(new(detector), new(client, detector), new(client, detector));
        using var source = new MemoryStream(bytes);
        var result = await router.NormalizeAsync(source, format, default);
        await using var pdf = result.Content;
        Assert.Equal("GotenbergLibreOffice", result.Producer);
        Assert.Equal(pdfBytes, ((MemoryStream)pdf).ToArray());
        Assert.True(source.CanRead);
        Assert.Equal(bytes, source.ToArray());
    }

    [Theory]
    [InlineData(400, DocumentFailureCode.ConversionFailed)]
    [InlineData(503, DocumentFailureCode.ConverterUnavailable)]
    [InlineData(500, DocumentFailureCode.ConverterUnavailable)]
    [InlineData(429, DocumentFailureCode.ConverterUnavailable)]
    [InlineData(504, DocumentFailureCode.ConversionTimeout)]
    [InlineData(408, DocumentFailureCode.ConversionTimeout)]
    public async Task MapsHttpErrors(int status, DocumentFailureCode code)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Response((HttpStatusCode)status, "Internal converter details"u8.ToArray()));
        using var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://converter/") };
        using var source = new MemoryStream(OfficeFixture.Create(DocumentFormat.Docx));
        var ex = await Assert.ThrowsAsync<DocumentProcessingException>(() => new GotenbergLibreOfficeNormalizer(client, new DocumentFormatDetector()).NormalizeAsync(source, DocumentFormat.Docx, default));
        Assert.Equal(code, ex.Code);
        Assert.DoesNotContain("Internal converter details", ex.Message);
    }

    [Theory]
    [InlineData(true, DocumentFailureCode.ConversionTimeout)]
    [InlineData(false, DocumentFailureCode.ConverterUnavailable)]
    public async Task MapsTimeoutAndNetworkFailure(bool timeout, DocumentFailureCode code)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(timeout ? new TaskCanceledException("timeout") : new HttpRequestException("connection refused"));
        using var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://converter/") };
        using var source = new MemoryStream(OfficeFixture.Create(DocumentFormat.Pptx));
        var ex = await Assert.ThrowsAsync<DocumentProcessingException>(() => new GotenbergLibreOfficeNormalizer(client, new DocumentFormatDetector()).NormalizeAsync(source, DocumentFormat.Pptx, default));
        Assert.Equal(code, ex.Code);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/html")]
    public async Task RejectsInvalidOutput(string mime)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Response(HttpStatusCode.OK, "%PDF-invalid"u8.ToArray(), mime));
        using var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://converter/") };
        using var source = new MemoryStream(OfficeFixture.Create(DocumentFormat.Docx));
        var ex = await Assert.ThrowsAsync<DocumentProcessingException>(() => new GotenbergLibreOfficeNormalizer(client, new DocumentFormatDetector()).NormalizeAsync(source, DocumentFormat.Docx, default));
        Assert.Equal(DocumentFailureCode.ConversionFailed, ex.Code);
    }

    [Fact]
    public async Task CallerCancellationIsNotConversionFailure()
    {
        var handler = new Mock<HttpMessageHandler>();
        using var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://converter/") };
        using var source = new MemoryStream(OfficeFixture.Create(DocumentFormat.Docx));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GotenbergLibreOfficeNormalizer(client, new DocumentFormatDetector())
            .NormalizeAsync(source, DocumentFormat.Docx, new CancellationToken(true)));
        handler.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    private static HttpResponseMessage Response(HttpStatusCode status, byte[] bytes, string mime = "application/pdf")
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        return response;
    }
}
