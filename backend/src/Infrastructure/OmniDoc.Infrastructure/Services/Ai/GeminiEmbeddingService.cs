using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.Infrastructure.Services.Ai;

public sealed class GeminiEmbeddingService : IEmbeddingService
{
    private const int EmbeddingDimensions = 768;
    private const int MaxErrorDetailsLength = 2_000;

    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;

    public GeminiEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IOptions<AiSettings> options)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClientFactory.CreateClient("GeminiClient");
        _settings = options.Value.Gemini;
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateConfiguration();

        var modelName = GetModelResourceName();
        var payload = new
        {
            model = modelName,
            content = CreateContent(text)
        };

        using var request = CreateRequest(
            BuildEndpoint("embedContent"),
            payload);

        using var response = await SendAsync(
            request,
            "embedding",
            cancellationToken);

        var responseBody = await ReadSuccessfulResponseAsync(
            response,
            "embedding",
            cancellationToken);

        return ParseEmbedding(responseBody, "embedding");
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return [];
        }

        ValidateConfiguration();

        var modelName = GetModelResourceName();
        var payload = new
        {
            requests = texts.Select(text => new
            {
                model = modelName,
                content = CreateContent(text ?? throw new ArgumentException(
                    "Embedding input cannot contain null values.",
                    nameof(texts)))
            }).ToArray()
        };

        using var request = CreateRequest(
            BuildEndpoint("batchEmbedContents"),
            payload);

        using var response = await SendAsync(
            request,
            "batch embedding",
            cancellationToken);

        var responseBody = await ReadSuccessfulResponseAsync(
            response,
            "batch embedding",
            cancellationToken);

        return ParseBatchEmbeddings(responseBody, texts.Count);
    }

    private static object CreateContent(string text) => new
    {
        parts = new[]
        {
            new { text }
        }
    };

    private static HttpRequestMessage CreateRequest<T>(string endpoint, T payload) =>
        new(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException(
                $"Gemini {operation} request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new HttpRequestException(
                $"Gemini {operation} request failed: {exception.Message}",
                exception,
                exception.StatusCode);
        }
    }

    private static async Task<string> ReadSuccessfulResponseAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return responseBody;
        }

        throw new HttpRequestException(
            $"Gemini {operation} request failed with HTTP {(int)response.StatusCode} " +
            $"({response.ReasonPhrase}): {ExtractErrorDetails(responseBody)}",
            null,
            response.StatusCode);
    }

    private static float[] ParseEmbedding(string responseBody, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);

            if (!document.RootElement.TryGetProperty(propertyName, out var embedding))
            {
                throw new HttpRequestException(
                    $"Gemini embedding response did not contain '{propertyName}'.");
            }

            return ParseValues(embedding);
        }
        catch (JsonException exception)
        {
            throw new HttpRequestException(
                $"Gemini embedding response contained invalid JSON: {exception.Message}",
                exception);
        }
    }

    private static IReadOnlyList<float[]> ParseBatchEmbeddings(
        string responseBody,
        int expectedCount)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);

            if (!document.RootElement.TryGetProperty("embeddings", out var embeddingsElement) ||
                embeddingsElement.ValueKind != JsonValueKind.Array)
            {
                throw new HttpRequestException(
                    "Gemini batch embedding response did not contain an 'embeddings' array.");
            }

            var embeddings = embeddingsElement
                .EnumerateArray()
                .Select(ParseValues)
                .ToList();

            if (embeddings.Count != expectedCount)
            {
                throw new HttpRequestException(
                    $"Gemini batch embedding response returned {embeddings.Count} vectors " +
                    $"for {expectedCount} inputs.");
            }

            return embeddings;
        }
        catch (JsonException exception)
        {
            throw new HttpRequestException(
                $"Gemini batch embedding response contained invalid JSON: {exception.Message}",
                exception);
        }
    }

    private static float[] ParseValues(JsonElement embeddingElement)
    {
        if (!embeddingElement.TryGetProperty("values", out var valuesElement) ||
            valuesElement.ValueKind != JsonValueKind.Array)
        {
            throw new HttpRequestException(
                "Gemini embedding response did not contain a 'values' array.");
        }

        var values = valuesElement
            .EnumerateArray()
            .Select(value => value.GetSingle())
            .ToArray();

        if (values.Length != EmbeddingDimensions)
        {
            throw new HttpRequestException(
                $"Gemini embedding response returned {values.Length} dimensions; " +
                $"{EmbeddingDimensions} were expected.");
        }

        return values;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new HttpRequestException(
                "Gemini API key is not configured. Set 'AiSettings:Gemini:ApiKey'.");
        }

        if (string.IsNullOrWhiteSpace(_settings.EmbeddingModel))
        {
            throw new HttpRequestException(
                "Gemini embedding model is not configured. Set 'AiSettings:Gemini:EmbeddingModel'.");
        }
    }

    private string BuildEndpoint(string operation) =>
        $"v1beta/models/{Uri.EscapeDataString(GetModelId())}:{operation}" +
        $"?key={Uri.EscapeDataString(_settings.ApiKey)}";

    private string GetModelResourceName() => $"models/{GetModelId()}";

    private string GetModelId() =>
        _settings.EmbeddingModel.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? _settings.EmbeddingModel["models/".Length..]
            : _settings.EmbeddingModel;

    private static string ExtractErrorDetails(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return "The response body was empty.";
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);

            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return Truncate(message.GetString() ?? responseBody);
            }
        }
        catch (JsonException)
        {
            // Preserve the provider body below when it is not JSON.
        }

        return Truncate(responseBody);
    }

    private static string Truncate(string value) =>
        value.Length <= MaxErrorDetailsLength
            ? value
            : value[..MaxErrorDetailsLength] + "...";
}
