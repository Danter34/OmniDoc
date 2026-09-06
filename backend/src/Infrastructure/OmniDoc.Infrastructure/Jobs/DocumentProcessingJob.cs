using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Domain.Exceptions;

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
    private readonly IDocumentNormalizer _normalizer;
    private readonly IDocumentArtifactStorage _artifacts;

    public DocumentProcessingJob(
        IApplicationDbContext dbContext,
        IFileStorageService fileStorage,
        IPdfParserService pdfParser,
        ITextChunkerService chunker,
        IEmbeddingService embeddingService,
        IDocumentProgressNotifier notifier,
        ILogger<DocumentProcessingJob> logger,
        IDocumentNormalizer normalizer,
        IDocumentArtifactStorage artifacts)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _pdfParser = pdfParser;
        _chunker = chunker;
        _embeddingService = embeddingService;
        _notifier = notifier;
        _logger = logger;
        _normalizer = normalizer;
        _artifacts = artifacts;
    }

    public async Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .Include(d => d.Artifacts)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} was not found; skipping ingestion.", documentId);
            return;
        }

        // A retried/completed job must not duplicate chunks or regenerate evidence.
        if (document.Status == DocumentStatus.Indexed) return;

        document.Status = DocumentStatus.Processing;
        document.ErrorMessage = null;
        document.FailureCode = null;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var pendingChunks = new List<DocumentChunk>();

        try
        {
            if (document.CanonicalArtifactId is null && document.DetectedFormat != DocumentFormat.Pdf)
            {
                await NotifyAsync(document, 5, DocumentProcessingStage.Validating, cancellationToken);
                var source = document.Artifacts.SingleOrDefault(a => a.Id == document.SourceArtifactId && a.Kind == ArtifactKind.Source)
                    ?? throw new InvalidDataException("Source artifact is missing.");
                await using var sourceStream = await _artifacts.OpenAsync(source, cancellationToken)
                    ?? throw new FileNotFoundException($"Stored file '{source.StoragePath}' is missing.");
                await NotifyAsync(document, 30, DocumentProcessingStage.Normalizing, cancellationToken);
                var normalized = await _normalizer.NormalizeAsync(sourceStream, document.DetectedFormat, cancellationToken);
                await using var pdf = normalized.Content;
                var artifact = await _artifacts.SaveAsync(pdf, document.WorkspaceId, document.Id,
                    ArtifactKind.CanonicalPdf, Path.ChangeExtension(source.FileName, ".pdf"), "application/pdf", normalized.Producer, cancellationToken);
                document.AddArtifact(artifact);
                _dbContext.DocumentArtifacts.Add(artifact);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            var canonical = document.Artifacts.SingleOrDefault(a => a.Id == document.CanonicalArtifactId && a.Kind == ArtifactKind.CanonicalPdf);
            if (canonical is null && (document.DetectedFormat != DocumentFormat.Pdf || document.CanonicalArtifactId is not null))
                throw new InvalidDataException("Canonical PDF artifact is missing.");
            var storagePath = canonical?.StoragePath ?? document.StoragePath;
            var extractionProgress = document.DetectedFormat == DocumentFormat.Pdf ? 10 : 40;
            await NotifyAsync(document, extractionProgress, DocumentProcessingStage.Extracting, cancellationToken);

            await using var fileStream = await _fileStorage.GetFileAsync(storagePath, cancellationToken)
                ?? throw new FileNotFoundException($"Stored file '{storagePath}' is missing.");

            var pages = await _pdfParser.ExtractPagesAsync(fileStream, cancellationToken);

            await NotifyAsync(document, document.DetectedFormat == DocumentFormat.Pdf ? 30 : 45, DocumentProcessingStage.Extracting, cancellationToken);

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
            document.ProcessingStage = ProcessingStage.Completed;
            document.ProgressPercentage = 100;
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
            document.FailureCode = ex is DocumentProcessingException failure ? failure.Code.ToString() : document.ProcessingStage switch
            {
                ProcessingStage.Normalizing => "NORMALIZATION_FAILED",
                ProcessingStage.Validating => "VALIDATION_FAILED",
                ProcessingStage.Extracting => "EXTRACTION_FAILED",
                _ => "INDEXING_FAILED"
            };
            document.ErrorMessage = ex.Message;
            document.ChunkCount = 0;
            document.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(CancellationToken.None);
            await NotifyAsync(document, -1, DocumentProcessingStage.Failed, CancellationToken.None, ex.Message);
        }
    }

    private static int ScaleEmbeddingProgress(int embedded, int total) =>
        EmbeddingProgressStart + (int)((double)embedded / total * (EmbeddingProgressEnd - EmbeddingProgressStart));

    private async Task NotifyAsync(
        Document document,
        int percentage,
        string stage,
        CancellationToken cancellationToken,
        string? errorMessage = null)
    {
        document.ProcessingStage = Enum.Parse<ProcessingStage>(stage);
        document.ProgressPercentage = percentage;
        // Completion was committed atomically with chunks above. Do not introduce
        // a second database failure after successful indexing.
        if (stage != DocumentProcessingStage.Completed)
            await _dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            await _notifier.NotifyProgressAsync(
                new DocumentProgressNotification(document.Id, document.WorkspaceId, percentage, stage, errorMessage),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { _logger.LogWarning(ex, "Could not publish document progress for {DocumentId}.", document.Id); }
    }
}
