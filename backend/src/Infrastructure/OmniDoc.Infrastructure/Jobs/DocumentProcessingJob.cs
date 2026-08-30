using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Infrastructure.Jobs;

public class DocumentProcessingJob : IDocumentProcessingJob
{
    private const int EmbeddingBatchSize = 16;
    private const int EmbeddingProgressStart = 50;
    private const int EmbeddingProgressEnd = 90;

    private readonly IApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorage;
    private readonly IPdfParserService _pdfParser;
    private readonly ITextChunkerService _chunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentProgressNotifier _notifier;
    private readonly ILogger<DocumentProcessingJob> _logger;

    public DocumentProcessingJob(
        IApplicationDbContext dbContext,
        IFileStorageService fileStorage,
        IPdfParserService pdfParser,
        ITextChunkerService chunker,
        IEmbeddingService embeddingService,
        IDocumentProgressNotifier notifier,
        ILogger<DocumentProcessingJob> logger)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _pdfParser = pdfParser;
        _chunker = chunker;
        _embeddingService = embeddingService;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} was not found; skipping ingestion.", documentId);
            return;
        }

        document.Status = DocumentStatus.Processing;
        document.ErrorMessage = null;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var pendingChunks = new List<DocumentChunk>();

        try
        {
            await NotifyAsync(document, 10, DocumentProcessingStage.Extracting, cancellationToken);

            await using var fileStream = await _fileStorage.GetFileAsync(document.StoragePath, cancellationToken)
                ?? throw new FileNotFoundException($"Stored file '{document.StoragePath}' is missing.");

            var pages = await _pdfParser.ExtractPagesAsync(fileStream, cancellationToken);

            await NotifyAsync(document, 30, DocumentProcessingStage.Extracting, cancellationToken);

            var chunks = _chunker.ChunkPages(pages);

            if (chunks.Count == 0)
            {
                throw new InvalidOperationException("No extractable text was found in the document.");
            }

            await NotifyAsync(document, EmbeddingProgressStart, DocumentProcessingStage.Chunking, cancellationToken);

            for (var offset = 0; offset < chunks.Count; offset += EmbeddingBatchSize)
            {
                var batch = chunks.Skip(offset).Take(EmbeddingBatchSize).ToList();

                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                    batch.Select(c => c.Content).ToList(),
                    cancellationToken);

                if (embeddings.Count != batch.Count)
                {
                    throw new InvalidOperationException(
                        $"Embedding provider returned {embeddings.Count} vectors for {batch.Count} chunks.");
                }

                pendingChunks.AddRange(batch.Select((chunk, i) => new DocumentChunk
                {
                    DocumentId = document.Id,
                    ChunkIndex = chunk.ChunkIndex,
                    PageNumber = chunk.PageNumber,
                    Content = chunk.Content,
                    Embedding = embeddings[i]
                }));

                await NotifyAsync(
                    document,
                    ScaleEmbeddingProgress(pendingChunks.Count, chunks.Count),
                    DocumentProcessingStage.Embedding,
                    cancellationToken);
            }

            _dbContext.DocumentChunks.AddRange(pendingChunks);

            document.Status = DocumentStatus.Indexed;
            document.ChunkCount = pendingChunks.Count;
            document.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Indexed document {DocumentId} into {ChunkCount} chunks.", document.Id, pendingChunks.Count);

            await NotifyAsync(document, 100, DocumentProcessingStage.Completed, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ingestion failed for document {DocumentId}.", document.Id);

            // A failure inside SaveChangesAsync leaves the chunks tracked as Added;
            // detaching them keeps the recovery save from replaying the same insert.
            if (pendingChunks.Count > 0)
            {
                _dbContext.DocumentChunks.RemoveRange(pendingChunks);
            }

            document.Status = DocumentStatus.Failed;
            document.ErrorMessage = ex.Message;
            document.ChunkCount = 0;
            document.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(CancellationToken.None);
            await NotifyAsync(document, -1, DocumentProcessingStage.Failed, CancellationToken.None, ex.Message);
        }
    }

    private static int ScaleEmbeddingProgress(int embedded, int total) =>
        EmbeddingProgressStart + (int)((double)embedded / total * (EmbeddingProgressEnd - EmbeddingProgressStart));

    private Task NotifyAsync(
        Document document,
        int percentage,
        string stage,
        CancellationToken cancellationToken,
        string? errorMessage = null) =>
        _notifier.NotifyProgressAsync(
            new DocumentProgressNotification(document.Id, document.WorkspaceId, percentage, stage, errorMessage),
            cancellationToken);
}
