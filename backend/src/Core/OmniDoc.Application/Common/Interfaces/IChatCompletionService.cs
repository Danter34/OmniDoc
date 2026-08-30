namespace OmniDoc.Application.Common.Interfaces;

public record ChatPromptMessage(string Role, string Content);

public interface IChatCompletionService
{
    Task<string> GenerateResponseAsync(
        IReadOnlyList<ChatPromptMessage> messages,
        CancellationToken cancellationToken = default);
}
