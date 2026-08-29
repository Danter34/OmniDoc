using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Documents.DTOs;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Documents.Commands.ProcessDocument;

public record ProcessDocumentCommand(Guid DocumentId) : IRequest<Result<DocumentDto>>;

public class ProcessDocumentCommandHandler : IRequestHandler<ProcessDocumentCommand, Result<DocumentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IPdfParserService _pdfParser;
    private readonly ITextChunkerService _chunker;
    private readonly IEmbeddingService _embeddingService;

    public ProcessDocumentCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorage,
        IPdfParserService pdfParser,
        ITextChunkerService chunker,
        IEmbeddingService embeddingService)
    {
        _context = context;
        _fileStorage = fileStorage;
        _pdfParser = pdfParser;
        _chunker = chunker;
        _embeddingService = embeddingService;
    }

    public async Task<Result<DocumentDto>> Handle(ProcessDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result<DocumentDto>.Failure($"Document '{request.DocumentId}' was not found.", 404);
        }

        document.Status = DocumentStatus.Processing;
        document.ErrorMessage = null;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var pendingChunks = new List<DocumentChunk>();

        try
        {
            await using var fileStream = await _fileStorage.GetFileAsync(document.StoragePath, cancellationToken)
                ?? throw new FileNotFoundException($"Stored file '{document.StoragePath}' is missing.");

            var pages = await _pdfParser.ExtractPagesAsync(fileStream, cancellationToken);
            var chunks = _chunker.ChunkPages(pages);

            if (chunks.Count == 0)
            {
                throw new InvalidOperationException("No extractable text was found in the document.");
            }

            var embeddings = await _embeddingService.GenerateBatchEmbeddingsAsync(
                chunks.Select(c => c.Content).ToList(),
                cancellationToken);

            if (embeddings.Count != chunks.Count)
            {
                throw new InvalidOperationException(
                    $"Embedding provider returned {embeddings.Count} vectors for {chunks.Count} chunks.");
            }

            pendingChunks.AddRange(chunks.Select((chunk, i) => new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = chunk.ChunkIndex,
                PageNumber = chunk.PageNumber,
                Content = chunk.Content,
                Embedding = embeddings[i]
            }));

            _context.DocumentChunks.AddRange(pendingChunks);

            document.Status = DocumentStatus.Indexed;
            document.ChunkCount = pendingChunks.Count;
            document.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<DocumentDto>.Success(document.ToDto());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failure inside SaveChangesAsync leaves the chunks tracked as Added;
            // detaching them keeps the recovery save from replaying the same insert.
            if (pendingChunks.Count > 0)
            {
                _context.DocumentChunks.RemoveRange(pendingChunks);
            }

            document.Status = DocumentStatus.Failed;
            document.ErrorMessage = ex.Message;
            document.ChunkCount = 0;
            document.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(CancellationToken.None);

            return Result<DocumentDto>.Failure(ex.Message, 500);
        }
    }
}
