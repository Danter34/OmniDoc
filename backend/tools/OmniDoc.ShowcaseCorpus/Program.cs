using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Services;
using OmniDoc.Infrastructure.Services.Ai;

// Preparation is explicit; startup imports the verified bundle without calling Gemini.
if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--inspect"))
    throw new ArgumentException("Usage: OmniDoc.ShowcaseCorpus <corpus-directory> [--inspect]; set GEMINI_API_KEY and optionally GEMINI_EMBEDDING_MODEL.");
var output = Path.GetFullPath(args[0]);
var detector = new DocumentFormatDetector();
var parser = new PdfPigParserService();
var chunker = new RecursiveTextChunkerService();
var samples = new[]
{
    (Id: "b4987f7e-48cc-4ba5-a117-10ac4cbced21", Title: "Báo cáo Ổn định Tài chính ASEAN+3 2024 — AMRO", File: "AFSR2024_Highlights_Vietnamese.pdf", Pages: 2),
    (Id: "b4987f7e-48cc-4ba5-a117-10ac4cbced22", Title: "Triển vọng Kinh tế Khu vực ASEAN+3 2024 — AMRO", File: "Highlights-Booklet_Vietnamese.pdf", Pages: 4)
};
var prepared = new List<ShowcaseCorpusDocument>();
foreach (var sample in samples)
{
    var bytes = await File.ReadAllBytesAsync(Path.Combine(output, sample.File));
    using var source = new MemoryStream(bytes);
    var detected = await detector.DetectAsync(source, sample.File, default);
    if (detected.Format != DocumentFormat.Pdf) throw new InvalidDataException("AMRO sources must be PDFs.");
    source.Position = 0;
    var pages = await parser.ExtractPagesAsync(source);
    if (pages.Count != sample.Pages || !pages.Select(p => p.PageNumber).SequenceEqual(Enumerable.Range(1, sample.Pages)) ||
        pages.Any(p => string.IsNullOrWhiteSpace(p.Text)))
        throw new InvalidDataException($"{sample.File} must contain {sample.Pages} extractable pages, numbered from 1.");
    if (args.Length == 2)
        foreach (var page in pages) Console.WriteLine($"{sample.File} / Page {page.PageNumber}\n{page.Text}\n");
    var chunks = chunker.ChunkPages(pages);
    var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
    prepared.Add(new ShowcaseCorpusDocument(Guid.Parse(sample.Id), sample.Title, DocumentFormat.Pdf, detected.ContentType,
        sample.File, hash, sample.File, hash,
        chunks.Select(c => new ShowcaseCorpusChunk(c.ChunkIndex, c.PageNumber, c.Content, [])).ToList()));
}
if (args.Length == 2) return;

var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? throw new InvalidOperationException("Missing GEMINI_API_KEY");
using var client = new HttpClient { BaseAddress = new Uri("https://generativelanguage.googleapis.com/"), Timeout = TimeSpan.FromMinutes(2) };
var ai = new AiSettings { Provider = "Gemini", Gemini = new()
{
    ApiKey = key,
    // text-embedding-004 was shut down on 2026-01-14. Keep query and corpus models aligned.
    EmbeddingModel = Environment.GetEnvironmentVariable("GEMINI_EMBEDDING_MODEL") ?? "gemini-embedding-2"
} };
var embeddings = new GeminiEmbeddingService(new ClientFactory(client), Options.Create(ai));
var documents = new List<ShowcaseCorpusDocument>();
foreach (var entry in prepared)
{
    var vectors = await embeddings.GenerateEmbeddingsAsync(entry.Chunks.Select(c => c.Content).ToArray());
    var document = entry with { Chunks = entry.Chunks.Select((c, i) => c with { Embedding = vectors[i] }).ToList() };
    await document.ValidateAsync(output, detector, parser, chunker, default);
    documents.Add(document);
    Console.WriteLine($"Prepared {entry.SourceFile}: {entry.Chunks.Count} chunks with real 768-dimensional embeddings.");
}
var corpus = new ShowcaseCorpus("amro-2024-v1", ai.Provider, ai.Gemini.EmbeddingModel.Replace("models/", ""), 768, documents);
// Publish only after both documents have been fully validated.
var temporaryPath = Path.Combine(output, "manifest.json.tmp");
try
{
    await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(corpus, ShowcaseCorpus.JsonOptions));
    File.Move(temporaryPath, Path.Combine(output, "manifest.json"), overwrite: true);
}
finally
{
    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
}
Console.WriteLine("Corpus ready. No database was modified.");

sealed class ClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
