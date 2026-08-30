namespace OmniDoc.Application.Common.Interfaces;

public record ChatPromptMessage(string Role, string Content);

public interface IChatCompletionService
{
    Task<string> GenerateResponseAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        CancellationToken cancellationToken = default);

    /// Yields the answer as the provider produces it. Chunk boundaries are arbitrary and
    /// may split a citation tag in half, so callers should pipe this through
    /// CitationStreamStateMachine instead of forwarding it straight to the client.
    IAsyncEnumerable<string> StreamResponseAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        CancellationToken cancellationToken = default);
}
