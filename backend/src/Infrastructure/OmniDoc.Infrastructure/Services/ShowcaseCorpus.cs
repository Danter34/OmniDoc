using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.Infrastructure.Services;

public sealed record ShowcaseCorpus(string Version, string Provider, string EmbeddingModel, int Dimensions, List<ShowcaseCorpusDocument> Documents)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<ShowcaseCorpus> LoadAsync(string path, AiSettings ai, CancellationToken ct)
    {
        if (new FileInfo(path).Length > 10 * 1024 * 1024) throw new InvalidDataException("Showcase manifest is too large.");
        await using var stream = File.OpenRead(path);
        var corpus = await JsonSerializer.DeserializeAsync<ShowcaseCorpus>(stream, JsonOptions, ct)
            ?? throw new InvalidDataException("Showcase manifest is empty.");
        var expectedModel = ai.Provider == "Gemini" ? ai.Gemini.EmbeddingModel.Replace("models/", "", StringComparison.OrdinalIgnoreCase) : "mock-sha256-v1";
        if (corpus.Dimensions != 768 || corpus.Provider != ai.Provider || corpus.EmbeddingModel != expectedModel)
            throw new InvalidDataException("Showcase embedding provider/model/dimensions do not match the configured query embedding service. Rebuild the corpus first.");
        if (string.IsNullOrWhiteSpace(corpus.Version) || corpus.Documents is not { Count: >= 2 and <= 3 } ||
            corpus.Documents.Select(d => d.Id).Distinct().Count() != corpus.Documents.Count)
            throw new InvalidDataException("Showcase requires a version and 2-3 distinct sample documents.");
        return corpus;
    }
}

public sealed record ShowcaseCorpusDocument(Guid Id, string Title, DocumentFormat Format, string ContentType,
    string SourceFile, string SourceSha256, string CanonicalFile, string CanonicalSha256, List<ShowcaseCorpusChunk> Chunks)
{
    public async Task<(byte[] Source, byte[] Canonical)> ValidateAsync(string root, IDocumentFormatDetector detector,
        IPdfParserService parser, ITextChunkerService chunker, CancellationToken ct)
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Title) || Title.Length > 256 ||
            Format is not (DocumentFormat.Pdf or DocumentFormat.Docx or DocumentFormat.Pptx) || Chunks is not { Count: > 0 })
            throw new InvalidDataException("Invalid showcase document metadata.");
        var source = await ReadVerifiedAsync(root, SourceFile, SourceSha256, ct);
        var canonical = await ReadVerifiedAsync(root, CanonicalFile, CanonicalSha256, ct);
        using var sourceStream = new MemoryStream(source);
        var detected = await detector.DetectAsync(sourceStream, SourceFile, ct);
        if (detected.Format != Format || detected.ContentType != ContentType)
            throw new InvalidDataException("Showcase source format does not match its manifest.");
        using var pdf = new MemoryStream(canonical);
        var extracted = chunker.ChunkPages(await parser.ExtractPagesAsync(pdf, ct));
        if (extracted.Count != Chunks.Count) throw new InvalidDataException("Showcase chunks do not match the canonical PDF.");
        for (var i = 0; i < Chunks.Count; i++)
        {
            var chunk = Chunks[i];
            if (chunk.Index != extracted[i].ChunkIndex || chunk.Page != extracted[i].PageNumber || chunk.Content != extracted[i].Content ||
                chunk.Embedding is not { Length: 768 } || chunk.Embedding.Any(v => !float.IsFinite(v)) || !chunk.Embedding.Any(v => v != 0))
                throw new InvalidDataException("Showcase contains invalid evidence or embeddings.");
        }
        return (source, canonical);
    }

    private static async Task<byte[]> ReadVerifiedAsync(string root, string file, string hash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(file) || file != Path.GetFileName(file) || file.Contains('/') || file.Contains('\\'))
            throw new InvalidDataException("Corpus artifacts must be filenames within the manifest directory.");
        var path = Path.Combine(root, file);
        if (new FileInfo(path).Length > 50L * 1024 * 1024) throw new InvalidDataException("Corpus artifact exceeds 50 MB.");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Showcase artifact checksum mismatch: {file}.");
        return bytes;
    }
}

public sealed record ShowcaseCorpusChunk(int Index, int Page, string Content, float[] Embedding);
