using System.Security.Cryptography;
using System.Text;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.Infrastructure.Services;

/// Placeholder embedding provider: deterministic per input text, so the same text
/// always yields the same unit vector. Swap for a real provider once configured.
public class MockEmbeddingService : IEmbeddingService
{
    private const int Dimensions = 1536;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDeterministicUnitVector(text));
    }

    public Task<IReadOnlyList<float[]>> GenerateBatchEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var embeddings = new List<float[]>(texts.Count);

        foreach (var text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            embeddings.Add(CreateDeterministicUnitVector(text));
        }

        return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
    }

    private static float[] CreateDeterministicUnitVector(string text)
    {
        var seed = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        var random = new Random(BitConverter.ToInt32(seed, 0));

        var vector = new float[Dimensions];
        double sumOfSquares = 0;

        for (var i = 0; i < Dimensions; i++)
        {
            var value = random.NextDouble() * 2 - 1;
            vector[i] = (float)value;
            sumOfSquares += value * value;
        }

        var magnitude = Math.Sqrt(sumOfSquares);

        if (magnitude == 0)
        {
            vector[0] = 1f;
            return vector;
        }

        for (var i = 0; i < Dimensions; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }

        return vector;
    }
}
