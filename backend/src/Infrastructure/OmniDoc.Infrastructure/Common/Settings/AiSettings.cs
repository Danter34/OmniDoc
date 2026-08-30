namespace OmniDoc.Infrastructure.Common.Settings;

public sealed class AiSettings
{
    public const string SectionName = "AiSettings";

    public string Provider { get; set; } = "Mock";

    public GeminiSettings Gemini { get; set; } = new();
}

public sealed class GeminiSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string ChatModel { get; set; } = "gemini-2.5-flash";

    public string EmbeddingModel { get; set; } = "text-embedding-004";
}
