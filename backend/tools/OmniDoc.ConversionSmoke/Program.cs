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

var office = new GotenbergLibreOfficeNormalizer(client, new DocumentFormatDetector());
foreach (var (format, bytes, ratio) in new[]
{
    (DocumentFormat.Docx, OfficeSmokeFixtures.Word(), 0d),
    (DocumentFormat.Pptx, OfficeSmokeFixtures.Slides(true), 16d / 9),
    (DocumentFormat.Pptx, OfficeSmokeFixtures.Slides(false), 4d / 3)
})
{
    using var source = new MemoryStream(bytes);
    var result = await office.NormalizeAsync(source, format, default);
    await using var pdf = result.Content;
    using var parsed = UglyToad.PdfPig.PdfDocument.Open(pdf);
    var pages = parsed.GetPages().ToList();
    if (pages.Count != 2 || pages.Any(p => !p.Text.Contains("OmniDoc")) ||
        pages.Any(p => p.Text.Contains("HIDDEN_SECRET") || p.Text.Contains("SPEAKER_SECRET")))
        throw new InvalidOperationException($"{format}: page/hidden-slide/notes check failed.");
    if (ratio > 0 && pages.Any(p => Math.Abs(p.Width / p.Height - ratio) > 0.01))
        throw new InvalidOperationException("PPTX slide aspect ratio changed.");
    if (format == DocumentFormat.Docx && pages.Any(p => !p.Text.Contains("bằng chứng")))
        throw new InvalidOperationException("DOCX Unicode extraction failed.");
    Console.WriteLine($"{format}: {pages.Count} pages; canvas {pages[0].Width:F1}x{pages[0].Height:F1}; source preserved; notes/hidden slides absent.");
}

foreach (var (columns, latin1) in new[] { (3, false), (7, false), (3, true) })
{
    var header = "HeaderEvidence," + string.Join(',', Enumerable.Repeat("Value", columns - 1));
    var text = header + "\n" + string.Join('\n', Enumerable.Range(1, 180).Select(i => $"Row{i}," + string.Join(',', Enumerable.Repeat(latin1 ? "Café" : "Bằng chứng", columns - 1))));
    using var source = new MemoryStream((latin1 ? Encoding.Latin1 : Encoding.UTF8).GetBytes(text));
    var result = await new CsvCanonicalPdfNormalizer(normalizer).NormalizeAsync(source, DocumentFormat.Csv, default);
    await using var pdf = result.Content;
    using var parsed = UglyToad.PdfPig.PdfDocument.Open(pdf);
    var pages = parsed.GetPages().ToList();
    if (pages.Count < 2 || pages.Any(p => !p.Text.Contains("HeaderEvidence")) || !pages[^1].Text.Contains("Row180"))
        throw new InvalidOperationException("CSV header repetition or row pagination failed.");
    if (pages.Any(p => (p.Width > p.Height) != (columns > 6)))
        throw new InvalidOperationException("CSV orientation is incorrect.");
    if (!pages[0].Text.Contains(latin1 ? "Café" : "Bằng chứng")) throw new InvalidOperationException("CSV encoding failed.");
    Console.WriteLine($"CSV: {columns} columns, Latin1={latin1}, {pages.Count} pages; repeated headers, row 180 and orientation verified.");
}

using (var source = new MemoryStream(OfficeSmokeFixtures.Workbook()))
{
    var result = await office.NormalizeAsync(source, DocumentFormat.Xlsx, default);
    await using var pdf = result.Content;
    using var parsed = UglyToad.PdfPig.PdfDocument.Open(pdf);
    var pages = parsed.GetPages().ToList();
    var text = string.Join('\n', pages.Select(p => p.Text));
    if (pages.Count < 2 || !text.Contains("Row80") || !text.Contains("Bằng chứng") ||
        text.Contains("OUTSIDE_PRINT_AREA") || text.Contains("HIDDEN_SHEET_SECRET"))
        throw new InvalidOperationException("XLSX print area, page breaks or hidden-sheet behavior failed.");
    Console.WriteLine($"XLSX: {pages.Count} pages; print area, row 80, Vietnamese and hidden-sheet exclusion verified.");
}
