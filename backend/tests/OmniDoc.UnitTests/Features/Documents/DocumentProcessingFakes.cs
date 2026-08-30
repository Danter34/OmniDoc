using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.UnitTests.Features.Documents;

internal sealed class FakeFileStorageService : IFileStorageService
{
    public Stream? FileToReturn { get; init; } = new MemoryStream([1, 2, 3]);

    public Task<string> SaveFileAsync(Stream fileStream, string fileName, Guid workspaceId, CancellationToken cancellationToken = default) =>
        Task.FromResult($"workspaces/{workspaceId}/{fileName}");

    public Task<Stream?> GetFileAsync(string storagePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(FileToReturn);

    public Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class FakePdfParserService : IPdfParserService
{
    public IReadOnlyList<PdfPageContent> Pages { get; init; } = [];

    public Exception? ThrowOnParse { get; init; }

    public Task<IReadOnlyList<PdfPageContent>> ExtractPagesAsync(Stream pdfStream, CancellationToken cancellationToken = default) =>
        ThrowOnParse is not null
            ? Task.FromException<IReadOnlyList<PdfPageContent>>(ThrowOnParse)
            : Task.FromResult(Pages);
}

/// Emits one chunk per page so tests can control the chunk count exactly.
internal sealed class FakeTextChunkerService : ITextChunkerService
{
    public IReadOnlyList<TextChunkItem> ChunkPages(IReadOnlyList<PdfPageContent> pages, int maxChunkSize = 800, int chunkOverlap = 150) =>
        pages
            .Where(page => !string.IsNullOrWhiteSpace(page.Text))
            .Select((page, index) => new TextChunkItem(index, page.PageNumber, page.Text))
            .ToList();
}

internal sealed class FakeEmbeddingService : IEmbeddingService
{
    public const int Dimensions = 4;

    public List<int> BatchSizes { get; } = [];

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vector(text));

    public Task<IReadOnlyList<float[]>> GenerateBatchEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        BatchSizes.Add(texts.Count);

        return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Vector).ToList());
    }

    private static float[] Vector(string text) =>
        Enumerable.Range(0, Dimensions).Select(i => (float)((text.Length + i) % 10) / 10f).ToArray();
}

internal sealed class RecordingProgressNotifier : IDocumentProgressNotifier
{
    public List<DocumentProgressNotification> Notifications { get; } = [];

    public Task NotifyProgressAsync(DocumentProgressNotification notification, CancellationToken cancellationToken = default)
    {
        Notifications.Add(notification);

        return Task.CompletedTask;
    }
}
