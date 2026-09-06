using System.Text;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Services;

// Explicit opt-in smoke runner; does not join the unit suite or require a DB.
if (args.Length != 1) throw new ArgumentException("Usage: dotnet run --project backend/tools/OmniDoc.ConversionSmoke -- http://localhost:53007/");
using var client = new HttpClient { BaseAddress = new Uri(args[0]), Timeout = TimeSpan.FromSeconds(120), MaxResponseContentBufferSize = 60L * 1024 * 1024 };
var normalizer = new GotenbergChromiumNormalizer(client, new DocumentFormatDetector());
var parser = new PdfPigParserService();
foreach (var format in new[] { DocumentFormat.Txt, DocumentFormat.Markdown })
{
    var text = format == DocumentFormat.Txt
        ? string.Join("\n", Enumerable.Repeat("Tiếng Việt: bằng chứng đối soát tài liệu OmniDoc.", 180))
        : "# Tiếng Việt\n\n" + string.Join("\n\n", Enumerable.Repeat("## Bằng chứng\n\n**Đối soát tài liệu OmniDoc.**\n\n| Mục | Giá trị |\n| --- | --- |\n| Nguồn | Chính xác |", 30));
    using var source = new MemoryStream(Encoding.UTF8.GetBytes(text));
    var result = await normalizer.NormalizeAsync(source, format, default);
    await using var pdf = result.Content;
    var pages = await parser.ExtractPagesAsync(pdf);
    if (pages.Count < 2 || !pages.Any(p => p.Text.Contains("OmniDoc")) || !pages.Any(p => p.Text.Contains("đối soát", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"{format} pagination/Unicode extraction failed.");
    Console.WriteLine($"{format}: {pages.Count} pages; {pdf.Length} PDF bytes; Vietnamese text extracted successfully.");
}
