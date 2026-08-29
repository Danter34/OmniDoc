using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Features.Retrieval.DTOs;
using OmniDoc.Domain.Enums;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace OmniDoc.Infrastructure.Services;

public class VectorRetrievalService : IRetrievalService
{
    private readonly IApplicationDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<VectorRetrievalService> _logger;

    public VectorRetrievalService(
        IApplicationDbContext context,
        IEmbeddingService embeddingService,
        ILogger<VectorRetrievalService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchResultDto>> SearchSimilarChunksAsync(
        Guid workspaceId,
        string query,
        int topK = 5,
        float minSimilarityScore = 0.3f,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        // Pgvector's operators only bind against a Vector-typed parameter; passing the
        // raw float[] makes Postgres look for a non-existent "vector <=> real[]" operator.
        var parameter = new Vector(queryVector);

        var matches = await _context.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.Embedding != null
                && chunk.Document!.WorkspaceId == workspaceId
                && chunk.Document.Status == DocumentStatus.Indexed)
            .Select(chunk => new
            {
                ChunkId = chunk.Id,
                chunk.DocumentId,
                DocumentTitle = chunk.Document!.Title,
                chunk.PageNumber,
                chunk.Content,
                Distance = VectorDbFunctionsExtensions.CosineDistance(chunk.Embedding!, parameter)
            })
            .Where(match => 1 - match.Distance >= minSimilarityScore)
            .OrderBy(match => match.Distance)
            .Take(topK)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Vector search in workspace {WorkspaceId} returned {MatchCount} chunks (topK={TopK}, minScore={MinScore})",
            workspaceId, matches.Count, topK, minSimilarityScore);

        return matches
            .Select(match => new SearchResultDto(
                match.ChunkId,
                match.DocumentId,
                match.DocumentTitle,
                match.PageNumber,
                match.Content,
                (float)(1 - match.Distance)))
            .ToList();
    }
}
