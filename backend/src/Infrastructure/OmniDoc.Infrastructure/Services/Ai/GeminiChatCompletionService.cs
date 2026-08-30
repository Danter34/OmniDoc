using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.Infrastructure.Services.Ai;

public sealed class GeminiChatCompletionService : IChatCompletionService
{
    private const int MaxErrorDetailsLength = 2_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;

    public GeminiChatCompletionService(
        IHttpClientFactory httpClientFactory,
        IOptions<AiSettings> options)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClientFactory.CreateClient("GeminiClient");
        _settings = options.Value.Gemini;
    }

    public async Task<string> GenerateResponseAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ValidateConfiguration();

        using var request = CreateRequest(
            BuildEndpoint("generateContent"),
            BuildPayload(messages));

        using var response = await SendAsync(
            request,
            "chat completion",
            cancellationToken);

        var responseBody = await ReadSuccessfulResponseAsync(
            response,
            "chat completion",
            cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var text = ExtractCandidateText(document.RootElement);

            return !string.IsNullOrEmpty(text)
                ? text
                : throw new HttpRequestException(
                    "Gemini chat response did not contain candidate text.");
        }
        catch (JsonException exception)
        {
            throw new HttpRequestException(
                $"Gemini chat response contained invalid JSON: {exception.Message}",
                exception);
        }
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ValidateConfiguration();

        using var request = CreateRequest(
            BuildEndpoint("streamGenerateContent", useSse: true),
            BuildPayload(messages));

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await SendStreamingRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Gemini streaming chat request failed with HTTP {(int)response.StatusCode} " +
                $"({response.ReasonPhrase}): {ExtractErrorDetails(responseBody)}",
                null,
                response.StatusCode);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var eventData = line["data:".Length..].TrimStart();

            if (eventData.Length == 0)
            {
                continue;
            }

            if (string.Equals(eventData, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            string? text;

            try
            {
                using var document = JsonDocument.Parse(eventData);
                text = ExtractCandidateText(document.RootElement);
            }
            catch (JsonException exception)
            {
                throw new HttpRequestException(
                    $"Gemini streaming chat response contained invalid SSE JSON: {exception.Message}",
                    exception);
            }

            if (!string.IsNullOrEmpty(text))
            {
                yield return text;
            }
        }
    }

    private static HttpRequestMessage CreateRequest<T>(string endpoint, T payload) =>
        new(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
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

    private async Task<HttpResponseMessage> SendStreamingRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException(
                "Gemini streaming chat request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new HttpRequestException(
                $"Gemini streaming chat request failed: {exception.Message}",
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

    private static GeminiGenerateRequest BuildPayload(
        IReadOnlyList<ChatPromptMessage> messages)
    {
        var systemText = string.Join(
            "\n\n",
            messages
                .Where(message => IsRole(message.Role, "System"))
                .Select(message => message.Content));

        var contents = messages
            .Where(message => !IsRole(message.Role, "System"))
            .Select(message => new GeminiContent(
                MapRole(message.Role),
                [new GeminiPart(message.Content)]))
            .ToList();

        return new GeminiGenerateRequest(
            contents,
            string.IsNullOrEmpty(systemText)
                ? null
                : new GeminiSystemInstruction([new GeminiPart(systemText)]));
    }

    private static string MapRole(string role)
    {
        if (IsRole(role, "User"))
        {
            return "user";
        }

        if (IsRole(role, "Assistant") || IsRole(role, "Model"))
        {
            return "model";
        }

        throw new ArgumentException(
            $"Unsupported Gemini chat role '{role}'. Expected System, User, or Assistant.",
            nameof(role));
    }

    private static bool IsRole(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static string? ExtractCandidateText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var candidate = candidates[0];

        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var textParts = parts
            .EnumerateArray()
            .Where(part => part.TryGetProperty("text", out _))
            .Select(part => part.GetProperty("text").GetString())
            .Where(text => !string.IsNullOrEmpty(text));

        return string.Concat(textParts);
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new HttpRequestException(
                "Gemini API key is not configured. Set 'AiSettings:Gemini:ApiKey'.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ChatModel))
        {
            throw new HttpRequestException(
                "Gemini chat model is not configured. Set 'AiSettings:Gemini:ChatModel'.");
        }
    }

    private string BuildEndpoint(string operation, bool useSse = false)
    {
        var endpoint =
            $"v1beta/models/{Uri.EscapeDataString(GetModelId())}:{operation}?";

        if (useSse)
        {
            endpoint += "alt=sse&";
        }

        return endpoint + $"key={Uri.EscapeDataString(_settings.ApiKey)}";
    }

    private string GetModelId() =>
        _settings.ChatModel.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? _settings.ChatModel["models/".Length..]
            : _settings.ChatModel;

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

    private sealed record GeminiGenerateRequest(
        [property: JsonPropertyName("contents")]
        IReadOnlyList<GeminiContent> Contents,
        [property: JsonPropertyName("system_instruction")]
        GeminiSystemInstruction? SystemInstruction);

    private sealed record GeminiSystemInstruction(
        [property: JsonPropertyName("parts")]
        IReadOnlyList<GeminiPart> Parts);

    private sealed record GeminiContent(
        [property: JsonPropertyName("role")]
        string Role,
        [property: JsonPropertyName("parts")]
        IReadOnlyList<GeminiPart> Parts);

    private sealed record GeminiPart(
        [property: JsonPropertyName("text")]
        string Text);
}
