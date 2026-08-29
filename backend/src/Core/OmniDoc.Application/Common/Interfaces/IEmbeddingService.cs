namespace OmniDoc.Application.Common.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> GenerateBatchEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
