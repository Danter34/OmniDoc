using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Moq;
using Moq.Protected;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Services;

namespace OmniDoc.UnitTests.Features.Documents;

public sealed class CsvNormalizationTests
{
    [Theory]
    [InlineData("Name,Value\nTiếng Việt,42", ',')]
    [InlineData("Name;Value\nTiếng Việt;42", ';')]
    [InlineData("Name\tValue\nTiếng Việt\t42", '\t')]
    [InlineData(" \n\r\nName;Value\nTiếng Việt;42", ';')]
    public async Task DetectsDelimiterAndStreamsUnicodeRows(string text, char delimiter)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var rows = new List<string[]>();
        var info = await new CsvDocumentReader().ReadAsync(source, (row, _) => rows.Add(row), default);
        Assert.Equal(delimiter, info.Delimiter);
        Assert.Equal(2, info.ColumnCount);
        Assert.Equal(1, info.DataRows);
        Assert.Equal(new[] { "Tiếng Việt", "42" }, rows[1]);
        Assert.Equal(0, source.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetectsUtf8WithOrWithoutBom(bool bom)
    {
        var encoding = new UTF8Encoding(bom);
        using var source = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes("Tên,Giá\nCà phê,42")).ToArray());
        var result = await new DocumentFormatDetector().DetectAsync(source, "data.CSV", default);
        Assert.Equal(DocumentFormat.Csv, result.Format);
        Assert.Equal("text/csv; charset=utf-8", result.ContentType);
        Assert.Equal(0, source.Position);
    }

    [Fact]
    public async Task DetectsLatin1AndPreservesBytesAndHash()
    {
        var bytes = Encoding.Latin1.GetBytes("Name;Price\r\nCafé;12,50");
        using var source = new MemoryStream(bytes);
        var rows = new List<string[]>();
        var info = await new CsvDocumentReader().ReadAsync(source, (row, _) => rows.Add(row), default);
        var detected = await new DocumentFormatDetector().DetectAsync(source, "latin.csv", default);
        Assert.Equal("text/csv; charset=iso-8859-1", info.ContentType);
        Assert.Equal(info.ContentType, detected.ContentType);
        Assert.Equal(new[] { "Café", "12,50" }, rows[1]);
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(source));
    }

    [Fact]
    public async Task ParsesQuotedDelimitersEscapedQuotesAndMultilineCells()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("\"Name, label\",Note\r\n\"A, B\",\"He said \"\"hello\"\"\r\nnext line\"\r\n"));
        var rows = new List<string[]>();
        var info = await new CsvDocumentReader().ReadAsync(source, (row, _) => rows.Add(row), default);
        Assert.Equal(1, info.DataRows);
        Assert.Equal("Name, label", rows[0][0]);
        Assert.Equal("A, B", rows[1][0]);
        Assert.Contains("He said \"hello\"", rows[1][1]);
        Assert.Contains("next line", rows[1][1]);
    }

    [Theory]
    [InlineData("A,B\n\"unterminated,42")]
    [InlineData("A,B\n1,2,3")]
    [InlineData("A,B\n1")]
    [InlineData("A,B;C\n1,2;3")]
    [InlineData("A,B\nx,\u0000")]
    [InlineData("%PDF-1.7\nA,B")]
    [InlineData("")]
    [InlineData(",\n1,2")]
    public async Task RejectsInvalidCsvWithoutConversion(string text)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(text));
        using var client = new HttpClient(new Mock<HttpMessageHandler>(MockBehavior.Strict).Object, false);
        var normalizer = new CsvCanonicalPdfNormalizer(new(client, new DocumentFormatDetector()));
        await Assert.ThrowsAsync<InvalidDataException>(() => normalizer.NormalizeAsync(source, DocumentFormat.Csv, default));
        Assert.Equal(0, source.Position);
    }

    [Fact]
    public async Task RejectsUtf16AndInvalidUtf8Bom()
    {
        foreach (var bytes in new[]
        {
            Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("A,B\n1,2")).ToArray(),
            new byte[] { 0xef, 0xbb, 0xbf, (byte)'A', (byte)',', 0xff }
        })
        {
            using var source = new MemoryStream(bytes);
            await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentFormatDetector().DetectAsync(source, "invalid.csv", default));
        }
    }

    [Theory]
    [InlineData(5000, true)]
    [InlineData(5001, false)]
    public async Task EnforcesRowLimitWithoutSilentTruncation(int count, bool accepted)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("A,B\n" + string.Join('\n', Enumerable.Repeat("1,2", count))));
        if (accepted)
            Assert.Equal(count, (await new CsvDocumentReader().ReadAsync(source, null, default)).DataRows);
        else await Assert.ThrowsAsync<InvalidDataException>(() => new CsvDocumentReader().ReadAsync(source, null, default));
    }

    [Fact]
    public async Task EnforcesCellAndColumnLimits()
    {
        foreach (var text in new[] { "A\n" + new string('x', 16385), string.Join(',', Enumerable.Repeat("column", 129)) })
        {
            using var source = new MemoryStream(Encoding.UTF8.GetBytes(text));
            await Assert.ThrowsAsync<InvalidDataException>(() => new CsvDocumentReader().ReadAsync(source, null, default));
        }
    }

    [Theory]
    [InlineData(6, "A4 portrait")]
    [InlineData(7, "A4 landscape")]
    public async Task RendersEscapedPrintTableWithRepeatedHeadersAndRowNumbers(int columns, string pageSize)
    {
        string? html = null;
        var pdfBytes = PdfFixture.Create();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
            {
                Assert.Equal("/forms/chromium/convert/html", request.RequestUri!.AbsolutePath);
                var form = Assert.IsType<MultipartFormDataContent>(request.Content);
                html = await form.Single(f => f.Headers.ContentDisposition?.FileName?.Trim('"') == "index.html").ReadAsStringAsync(ct);
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(pdfBytes) };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                return response;
            });
        using var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://converter/") };
        var header = "<script>header</script>," + string.Join(',', Enumerable.Repeat("Column", columns - 1));
        var row = "<img src=x onerror=alert(1)>," + string.Join(',', Enumerable.Repeat("Tiếng Việt & data", columns - 1));
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(header + "\n" + row + "\n" + row));
        var detector = new DocumentFormatDetector();
        var chromium = new GotenbergChromiumNormalizer(client, detector);
        var router = new DocumentNormalizer(new(detector), chromium, new(client, detector), new(chromium));
        var result = await router.NormalizeAsync(source, DocumentFormat.Csv, default);
        await using var pdf = result.Content;
        Assert.Equal(pdfBytes, ((MemoryStream)pdf).ToArray());
        Assert.Contains("size: " + pageSize, html);
        Assert.Contains("display: table-header-group", html);
        Assert.Contains("<thead>", html);
        Assert.Contains("<th scope=\"row\">1</th>", html);
        Assert.Contains("<th scope=\"row\">2</th>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img ", html);
        Assert.Contains("Tiếng Việt & data", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task HonorsCancellation()
    {
        using var source = new MemoryStream("A,B\n1,2"u8.ToArray());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CsvDocumentReader().ReadAsync(source, null, new CancellationToken(true)));
    }
}
