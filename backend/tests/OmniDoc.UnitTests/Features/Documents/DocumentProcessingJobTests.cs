using Microsoft.Extensions.Logging.Abstractions;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Jobs;

namespace OmniDoc.UnitTests.Features.Documents;

public class DocumentProcessingJobTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    private static Document SeedDocument(TestApplicationDbContext context)
    {
        var document = new Document
        {
            WorkspaceId = WorkspaceId,
            Title = "BaoCao",
            FileName = "BaoCao.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 2048,
            StoragePath = $"workspaces/{WorkspaceId}/BaoCao.pdf",
            Status = DocumentStatus.Pending
        };

        context.Documents.Add(document);
        context.SaveChanges();

        return document;
    }

    private static DocumentProcessingJob CreateJob(
        TestApplicationDbContext context,
        IPdfParserService parser,
        RecordingProgressNotifier notifier,
        IEmbeddingService? embeddingService = null,
        IFileStorageService? fileStorage = null) =>
        new(
            context,
            fileStorage ?? new FakeFileStorageService(),
            parser,
            new FakeTextChunkerService(),
            embeddingService ?? new FakeEmbeddingService(),
            notifier,
            NullLogger<DocumentProcessingJob>.Instance);

    private static FakePdfParserService ParserWithPages(int pageCount) =>
        new()
        {
            Pages = Enumerable.Range(1, pageCount)
                .Select(page => new PdfPageContent(page, $"Nội dung trang số {page}."))
                .ToList()
        };

    [Fact]
    public async Task ProcessDocumentAsync_IndexesDocumentAndPersistsChunks()
    {
        await using var context = new TestApplicationDbContext();
        var document = SeedDocument(context);
        var notifier = new RecordingProgressNotifier();

        await CreateJob(context, ParserWithPages(3), notifier).ProcessDocumentAsync(document.Id);

        Assert.Equal(DocumentStatus.Indexed, document.Status);
        Assert.Equal(3, document.ChunkCount);
        Assert.Null(document.ErrorMessage);
        Assert.NotNull(document.UpdatedAtUtc);

        var chunks = context.DocumentChunks.OrderBy(c => c.ChunkIndex).ToList();
        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, chunk =>
        {
            Assert.Equal(document.Id, chunk.DocumentId);
            Assert.Equal(FakeEmbeddingService.Dimensions, Assert.IsType<float[]>(chunk.Embedding).Length);
        });
        Assert.Equal([1, 2, 3], chunks.Select(c => c.PageNumber));
    }

    [Fact]
    public async Task ProcessDocumentAsync_EmitsProgressForEveryStage()
    {
        await using var context = new TestApplicationDbContext();
        var document = SeedDocument(context);
        var notifier = new RecordingProgressNotifier();

        await CreateJob(context, ParserWithPages(2), notifier).ProcessDocumentAsync(document.Id);

        Assert.All(notifier.Notifications, n =>
        {
            Assert.Equal(document.Id, n.DocumentId);
            Assert.Equal(WorkspaceId, n.WorkspaceId);
        });

        Assert.Equal(
            [
                (DocumentProcessingStage.Extracting, 10),
                (DocumentProcessingStage.Extracting, 30),
                (DocumentProcessingStage.Chunking, 50),
                (DocumentProcessingStage.Embedding, 90),
                (DocumentProcessingStage.Completed, 100)
            ],
            notifier.Notifications.Select(n => (n.Stage, n.ProgressPercentage)));
    }

    [Fact]
    public async Task ProcessDocumentAsync_EmbedsInBatchesWithRisingProgress()
    {
        await using var context = new TestApplicationDbContext();
        var document = SeedDocument(context);
        var notifier = new RecordingProgressNotifier();
        var embeddingService = new FakeEmbeddingService();

        await CreateJob(context, ParserWithPages(40), notifier, embeddingService).ProcessDocumentAsync(document.Id);

        Assert.Equal([16, 16, 8], embeddingService.BatchSizes);

        var embeddingProgress = notifier.Notifications
            .Where(n => n.Stage == DocumentProcessingStage.Embedding)
            .Select(n => n.ProgressPercentage)
            .ToList();

        Assert.Equal([66, 82, 90], embeddingProgress);
        Assert.Equal(40, document.ChunkCount);
    }

    [Fact]
    public async Task ProcessDocumentAsync_MarksDocumentFailedWhenParsingThrows()
    {
        await using var context = new TestApplicationDbContext();
        var document = SeedDocument(context);
        var notifier = new RecordingProgressNotifier();
        var parser = new FakePdfParserService { ThrowOnParse = new InvalidDataException("PDF header is corrupt.") };

        await CreateJob(context, parser, notifier).ProcessDocumentAsync(document.Id);

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("PDF header is corrupt.", document.ErrorMessage);
        Assert.Equal(0, document.ChunkCount);
        Assert.Empty(context.DocumentChunks);

        var failure = notifier.Notifications[^1];
        Assert.Equal(DocumentProcessingStage.Failed, failure.Stage);
        Assert.Equal(-1, failure.ProgressPercentage);
        Assert.Equal("PDF header is corrupt.", failure.ErrorMessage);
    }

    [Fact]
    public async Task ProcessDocumentAsync_FailsWhenStoredFileIsMissing()
    {
        await using var context = new TestApplicationDbContext();
        var document = SeedDocument(context);
        var notifier = new RecordingProgressNotifier();
        var storage = new FakeFileStorageService { FileToReturn = null };

        await CreateJob(context, ParserWithPages(1), notifier, fileStorage: storage).ProcessDocumentAsync(document.Id);

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Contains(document.StoragePath, document.ErrorMessage);
        Assert.Equal(DocumentProcessingStage.Failed, notifier.Notifications[^1].Stage);
    }

    [Fact]
    public async Task ProcessDocumentAsync_FailsWhenNoTextIsExtractable()
    {
        await using var context = new TestApplicationDbContext();
        var document = SeedDocument(context);
        var notifier = new RecordingProgressNotifier();
        var parser = new FakePdfParserService { Pages = [new PdfPageContent(1, "   ")] };

        await CreateJob(context, parser, notifier).ProcessDocumentAsync(document.Id);

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("No extractable text was found in the document.", document.ErrorMessage);
        Assert.DoesNotContain(notifier.Notifications, n => n.Stage == DocumentProcessingStage.Chunking);
    }

    [Fact]
    public async Task ProcessDocumentAsync_IgnoresUnknownDocument()
    {
        await using var context = new TestApplicationDbContext();
        var notifier = new RecordingProgressNotifier();

        await CreateJob(context, ParserWithPages(1), notifier).ProcessDocumentAsync(Guid.NewGuid());

        Assert.Empty(notifier.Notifications);
        Assert.Empty(context.Documents);
    }
}
