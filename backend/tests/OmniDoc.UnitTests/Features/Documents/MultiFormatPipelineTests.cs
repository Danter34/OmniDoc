using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Domain.Exceptions;
using OmniDoc.Infrastructure.Jobs;
using OmniDoc.Infrastructure.Services;

namespace OmniDoc.UnitTests.Features.Documents;

internal sealed class MemoryArtifactFiles : IFileStorageService
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public async Task<string> SaveFileAsync(Stream stream, string name, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes, cancellationToken);
        var path = $"{workspaceId}/{Guid.NewGuid()}/{name}";
        Files[path] = bytes.ToArray();
        return path;
    }
    public Task<Stream?> GetFileAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(Files.TryGetValue(path, out var bytes) ? new MemoryStream(bytes) : null);
    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default) { Files.Remove(path); return Task.CompletedTask; }
}

public sealed class MultiFormatPipelineTests
{
    [Theory]
    [InlineData(DocumentFormat.Txt)]
    [InlineData(DocumentFormat.Markdown)]
    [InlineData(DocumentFormat.Docx)]
    [InlineData(DocumentFormat.Pptx)]
    [InlineData(DocumentFormat.Xlsx)]
    [InlineData(DocumentFormat.Csv)]
    public async Task ProcessesCanonicalPdfAndPreservesSourceOnRedelivery(DocumentFormat format)
    {
        await using var context = new TestApplicationDbContext();
        var files = new MemoryArtifactFiles();
        var storage = new DocumentArtifactStorage(files);
        var doc = await Seed(context, storage, format);
        var original = files.Files[doc.StoragePath].ToArray();
        var pdf = PdfFixture.Create();
        var normalizer = new Mock<IDocumentNormalizer>();
        var producer = format is DocumentFormat.Docx or DocumentFormat.Pptx or DocumentFormat.Xlsx ? "GotenbergLibreOffice" : "GotenbergChromium";
        normalizer.Setup(n => n.NormalizeAsync(It.IsAny<Stream>(), format, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CanonicalPdfResult(new MemoryStream(pdf), producer));
        var notifier = new RecordingProgressNotifier();
        var job = Job(context, files, normalizer.Object, new FakeEmbeddingService(), notifier);
        await job.ProcessDocumentAsync(doc.Id);
        await job.ProcessDocumentAsync(doc.Id);
        Assert.Equal(DocumentStatus.Indexed, doc.Status);
        Assert.Equal(ProcessingStage.Completed, doc.ProcessingStage);
        Assert.Equal(100, doc.ProgressPercentage);
        Assert.Single(context.DocumentChunks);
        Assert.Equal(1, context.DocumentChunks.Single().PageNumber);
        Assert.Contains("Canonical evidence", context.DocumentChunks.Single().Content);
        Assert.Equal(original, files.Files[doc.StoragePath]);
        var canonical = doc.Artifacts.Single(a => a.Id == doc.CanonicalArtifactId);
        Assert.Equal(producer, canonical.Producer);
        Assert.Equal(pdf, files.Files[canonical.StoragePath]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(pdf)), canonical.Sha256);
        Assert.NotEqual(doc.StoragePath, canonical.StoragePath);
        Assert.Contains(notifier.Notifications, n => n.Stage == "Normalizing" && n.ProgressPercentage == 30);
        normalizer.Verify(n => n.NormalizeAsync(It.IsAny<Stream>(), format, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FailedEmbeddingRetryReusesCanonicalArtifact()
    {
        await using var context = new TestApplicationDbContext();
        var files = new MemoryArtifactFiles();
        var doc = await Seed(context, new DocumentArtifactStorage(files), DocumentFormat.Txt);
        var normalizer = new Mock<IDocumentNormalizer>();
        normalizer.Setup(n => n.NormalizeAsync(It.IsAny<Stream>(), DocumentFormat.Txt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CanonicalPdfResult(new MemoryStream(PdfFixture.Create()), "GotenbergChromium"));
        var brokenEmbedding = new Mock<IEmbeddingService>();
        brokenEmbedding.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Embedding unavailable"));
        await Job(context, files, normalizer.Object, brokenEmbedding.Object).ProcessDocumentAsync(doc.Id);
        Assert.Equal(DocumentStatus.Failed, doc.Status);
        var canonicalId = doc.CanonicalArtifactId;
        Assert.NotNull(canonicalId);
        Assert.Empty(context.DocumentChunks);
        await Job(context, files, normalizer.Object, new FakeEmbeddingService()).ProcessDocumentAsync(doc.Id);
        Assert.Equal(DocumentStatus.Indexed, doc.Status);
        Assert.Null(doc.FailureCode);
        Assert.Equal(canonicalId, doc.CanonicalArtifactId);
        Assert.Equal(2, files.Files.Count);
        normalizer.Verify(n => n.NormalizeAsync(It.IsAny<Stream>(), DocumentFormat.Txt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConversionFailurePreservesSourceWithoutPublishingCanonical()
    {
        await using var context = new TestApplicationDbContext();
        var files = new MemoryArtifactFiles();
        var doc = await Seed(context, new DocumentArtifactStorage(files), DocumentFormat.Txt);
        var normalizer = new Mock<IDocumentNormalizer>();
        normalizer.Setup(n => n.NormalizeAsync(It.IsAny<Stream>(), DocumentFormat.Txt, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Converter unavailable"));
        await Job(context, files, normalizer.Object, new FakeEmbeddingService()).ProcessDocumentAsync(doc.Id);
        Assert.Equal(DocumentStatus.Failed, doc.Status);
        Assert.Equal("NORMALIZATION_FAILED", doc.FailureCode);
        Assert.Null(doc.CanonicalArtifactId);
        Assert.Single(files.Files);
        Assert.Empty(context.DocumentChunks);
    }

    [Theory]
    [InlineData(DocumentFailureCode.ConversionTimeout)]
    [InlineData(DocumentFailureCode.ConverterUnavailable)]
    [InlineData(DocumentFailureCode.PasswordRequired)]
    public async Task PersistsTypedFailureCodeAndLeavesSourceIntact(DocumentFailureCode code)
    {
        await using var context = new TestApplicationDbContext();
        var files = new MemoryArtifactFiles();
        var doc = await Seed(context, new DocumentArtifactStorage(files), DocumentFormat.Docx);
        var original = files.Files[doc.StoragePath].ToArray();
        var normalizer = new Mock<IDocumentNormalizer>();
        normalizer.Setup(n => n.NormalizeAsync(It.IsAny<Stream>(), DocumentFormat.Docx, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DocumentProcessingException(code, "Office conversion failed"));
        await Job(context, files, normalizer.Object, new FakeEmbeddingService()).ProcessDocumentAsync(doc.Id);
        Assert.Equal(code.ToString(), context.Documents.Single().FailureCode);
        Assert.Equal(ProcessingStage.Failed, doc.ProcessingStage);
        Assert.Null(doc.CanonicalArtifactId);
        Assert.Equal(original, Assert.Single(files.Files).Value);
        Assert.Empty(context.DocumentChunks);
    }

    private static async Task<Document> Seed(TestApplicationDbContext context, IDocumentArtifactStorage storage, DocumentFormat format)
    {
        var fileName = format switch { DocumentFormat.Docx => "report.docx", DocumentFormat.Pptx => "slides.pptx", DocumentFormat.Xlsx => "table.xlsx", DocumentFormat.Csv => "table.csv", DocumentFormat.Txt => "note.txt", _ => "note.md" };
        var doc = new Document { WorkspaceId = Guid.NewGuid(), DetectedFormat = format, FileName = fileName };
        using var source = new MemoryStream(format is DocumentFormat.Docx or DocumentFormat.Pptx or DocumentFormat.Xlsx ? OfficeFixture.Create(format) : format == DocumentFormat.Csv ? "Name,Value\nEvidence,42"u8.ToArray() : "Original source text"u8.ToArray());
        var mime = format switch { DocumentFormat.Docx => OfficeFixture.DocxMime, DocumentFormat.Pptx => OfficeFixture.PptxMime, DocumentFormat.Xlsx => OfficeFixture.XlsxMime, DocumentFormat.Csv => "text/csv; charset=utf-8", _ => "text/plain" };
        var artifact = await storage.SaveAsync(source, doc.WorkspaceId, doc.Id, ArtifactKind.Source, doc.FileName, mime, "Upload", default);
        doc.AddArtifact(artifact);
        doc.StoragePath = artifact.StoragePath;
        context.Documents.Add(doc);
        await context.SaveChangesAsync();
        return doc;
    }

    private static DocumentProcessingJob Job(TestApplicationDbContext context, IFileStorageService files, IDocumentNormalizer normalizer,
        IEmbeddingService embeddings, IDocumentProgressNotifier? notifier = null) =>
        new(context, files, new PdfPigParserService(), new FakeTextChunkerService(), embeddings,
            notifier ?? new RecordingProgressNotifier(), NullLogger<DocumentProcessingJob>.Instance, normalizer, new DocumentArtifactStorage(files));
}
