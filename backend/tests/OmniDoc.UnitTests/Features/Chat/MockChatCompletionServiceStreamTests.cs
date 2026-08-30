using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Features.Chat.Services;
using OmniDoc.Infrastructure.Services;

namespace OmniDoc.UnitTests.Features.Chat;

/// The state machine can only guarantee no lost characters if the provider's chunks
/// reassemble into exactly the answer it meant to send. These tests pin that contract for
/// the mock provider, plus the fact that it actually splits citation tags apart.
public class MockChatCompletionServiceStreamTests
{
    private static readonly List<ChatPromptMessage> Prompt = RagPromptBuilder.BuildPrompt(
        StreamTestHelpers.Context,
        [],
        "Doanh thu quý một thế nào?");

    [Fact]
    public async Task StreamResponseAsync_ConcatenatedChunks_MatchSynchronousAnswer()
    {
        var service = new MockChatCompletionService();

        var expected = await service.GenerateResponseAsync(Prompt, CancellationToken.None);

        var chunks = await CollectAsync(service);

        Assert.Equal(expected, string.Concat(chunks));
    }

    [Fact]
    public async Task StreamResponseAsync_SplitsCitationTagsAcrossChunks()
    {
        var service = new MockChatCompletionService();

        var chunks = await CollectAsync(service);

        // If every tag arrived whole, the state machine's hardest path would go untested.
        Assert.Contains(chunks, chunk => chunk.Contains("[Doc", StringComparison.Ordinal)
            && !chunk.Contains(']', StringComparison.Ordinal));
    }

    [Fact]
    public async Task StreamResponseAsync_PipedThroughStateMachine_ResolvesEveryCitation()
    {
        var service = new MockChatCompletionService();
        var machine = new CitationStreamStateMachine();

        var events = new List<Application.Features.Chat.DTOs.ChatStreamEvent>();

        await foreach (var streamEvent in machine.ProcessStreamAsync(
            service.StreamResponseAsync(Prompt, CancellationToken.None),
            StreamTestHelpers.Context,
            CancellationToken.None))
        {
            events.Add(streamEvent);
        }

        // The mock cites each retrieved source exactly once.
        var citations = events.Citations();
        Assert.Equal(2, citations.Count);
        Assert.Equal(
            StreamTestHelpers.Context.Select(chunk => chunk.ChunkId).OrderBy(id => id),
            citations.Select(citation => citation.ChunkId).OrderBy(id => id));

        Assert.DoesNotContain("[Doc", events.VisibleText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamResponseAsync_NoContext_StillStreamsTheFallbackAnswer()
    {
        var service = new MockChatCompletionService();
        var prompt = RagPromptBuilder.BuildPrompt([], [], "Câu hỏi không có nguồn");

        var expected = await service.GenerateResponseAsync(prompt, CancellationToken.None);
        var chunks = await CollectAsync(service, prompt);

        Assert.NotEmpty(chunks);
        Assert.Equal(expected, string.Concat(chunks));
    }

    [Fact]
    public async Task StreamResponseAsync_CancelledToken_Throws()
    {
        var service = new MockChatCompletionService();
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in service.StreamResponseAsync(Prompt, cts.Token))
            {
            }
        });
    }

    private static async Task<List<string>> CollectAsync(
        MockChatCompletionService service,
        IReadOnlyList<ChatPromptMessage>? prompt = null)
    {
        var chunks = new List<string>();

        await foreach (var chunk in service.StreamResponseAsync(
            prompt ?? Prompt,
            CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        return chunks;
    }
}
