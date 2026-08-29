using OmniDoc.Application.Features.Retrieval.DTOs;

namespace OmniDoc.Application.Common.Interfaces;

public interface IRetrievalService
{
    Task<IReadOnlyList<SearchResultDto>> SearchSimilarChunksAsync(
        Guid workspaceId,
        string query,
        int topK = 5,
        float minSimilarityScore = 0.3f,
        CancellationToken cancellationToken = default);
}
