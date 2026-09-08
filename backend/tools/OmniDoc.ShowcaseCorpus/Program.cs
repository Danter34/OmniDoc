using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Services;
using OmniDoc.Infrastructure.Services.Ai;

// Explicit preparation, never invoked by startup/login. Calls Gotenberg and Gemini once.
if (args.Length != 2) throw new ArgumentException("Usage: OmniDoc.ShowcaseCorpus <gotenberg-url> <output-directory>; set GEMINI_API_KEY and optionally GEMINI_EMBEDDING_MODEL.");
var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("GEMINI_API_KEY is required. Mock vectors are not suitable for the public corpus.");
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
using var conversionClient = new HttpClient { BaseAddress = new Uri(args[0]), Timeout = TimeSpan.FromMinutes(2), MaxResponseContentBufferSize = 60L * 1024 * 1024 };
using var embeddingClient = new HttpClient { BaseAddress = new Uri("https://generativelanguage.googleapis.com/"), Timeout = TimeSpan.FromMinutes(2) };
var ai = new AiSettings { Provider = "Gemini", Gemini = new() { ApiKey = key, EmbeddingModel = Environment.GetEnvironmentVariable("GEMINI_EMBEDDING_MODEL") ?? "gemini-embedding-2" } };
var embeddings = new GeminiEmbeddingService(new ClientFactory(embeddingClient), Options.Create(ai));
var detector = new DocumentFormatDetector();
var chromium = new GotenbergChromiumNormalizer(conversionClient, detector);
var office = new GotenbergLibreOfficeNormalizer(conversionClient, detector);
var parser = new PdfPigParserService();
var chunker = new RecursiveTextChunkerService();
var documents = new List<ShowcaseCorpusDocument>();
using var reportText = new MemoryStream(Encoding.UTF8.GetBytes(SampleSources.Report));
var report = await chromium.NormalizeAsync(reportText, DocumentFormat.Markdown, default);
await using var reportPdf = report.Content;
using var reportBuffer = new MemoryStream();
await reportPdf.CopyToAsync(reportBuffer);
var samples = new[]
{
    (Id: "b4987f7e-48cc-4ba5-a117-10ac4cbced11", Title: "Báo cáo vận hành Quý III — Northstar", File: "northstar-report.pdf", Format: DocumentFormat.Pdf, Bytes: reportBuffer.ToArray()),
    (Id: "b4987f7e-48cc-4ba5-a117-10ac4cbced12", Title: "Quy trình mua sắm — Northstar", File: "northstar-procurement.docx", Format: DocumentFormat.Docx, Bytes: SampleSources.Word()),
    (Id: "b4987f7e-48cc-4ba5-a117-10ac4cbced13", Title: "Kế hoạch triển khai — Northstar", File: "northstar-rollout.pptx", Format: DocumentFormat.Pptx, Bytes: SampleSources.Slides())
};
foreach (var sample in samples)
{
    using var source = new MemoryStream(sample.Bytes);
    var detected = await detector.DetectAsync(source, sample.File, default);
    source.Position = 0;
    byte[] pdfBytes;
    if (sample.Format == DocumentFormat.Pdf) pdfBytes = sample.Bytes;
    else
    {
        var result = await office.NormalizeAsync(source, sample.Format, default);
        await using var pdf = result.Content;
        using var buffer = new MemoryStream();
        await pdf.CopyToAsync(buffer);
        pdfBytes = buffer.ToArray();
    }
    using var evidence = new MemoryStream(pdfBytes);
    var chunks = chunker.ChunkPages(await parser.ExtractPagesAsync(evidence));
    if (chunks.Count == 0) throw new InvalidDataException("Sample contains no extractable evidence.");
    var vectors = await embeddings.GenerateEmbeddingsAsync(chunks.Select(c => c.Content).ToArray());
    var pdfName = Path.GetFileNameWithoutExtension(sample.File) + ".canonical.pdf";
    await File.WriteAllBytesAsync(Path.Combine(output, sample.File), sample.Bytes);
    await File.WriteAllBytesAsync(Path.Combine(output, pdfName), pdfBytes);
    var entry = new ShowcaseCorpusDocument(Guid.Parse(sample.Id), sample.Title, sample.Format, detected.ContentType,
        sample.File, Convert.ToHexStringLower(SHA256.HashData(sample.Bytes)), pdfName, Convert.ToHexStringLower(SHA256.HashData(pdfBytes)),
        chunks.Select((c, i) => new ShowcaseCorpusChunk(c.ChunkIndex, c.PageNumber, c.Content, vectors[i])).ToList());
    await entry.ValidateAsync(output, detector, parser, chunker, default);
    documents.Add(entry);
    Console.WriteLine($"Prepared {sample.File}: {chunks.Count} chunks with real 768-dimensional embeddings.");
}
var corpus = new ShowcaseCorpus("northstar-v1", ai.Provider, ai.Gemini.EmbeddingModel.Replace("models/", ""), 768, documents);
await File.WriteAllTextAsync(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(corpus, ShowcaseCorpus.JsonOptions));
Console.WriteLine("Corpus ready. No database was modified.");

sealed class ClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
