using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Moq;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Chat.DTOs;
using OmniDoc.Application.Features.Chat.Services;
using OmniDoc.Application.Features.Chat.Streaming.StreamMessage;
using OmniDoc.Domain.Enums;
using OmniDoc.UnitTests.Features.Documents;

namespace OmniDoc.UnitTests.Features.Chat;

public sealed class ProviderFailureTests
{
    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    public async Task StreamingProviderFailure_PreservesPartialAnswerAndReturnsSafeTerminalError(int status)
    {
        await using var db = new TestApplicationDbContext();
        var workspaceId = Guid.NewGuid();
        var retrieval = new Mock<IRetrievalService>();
        retrieval.Setup(x => x.SearchSimilarChunksAsync(workspaceId, It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var authorization = new Mock<IWorkspaceAuthorizationService>();
        authorization.Setup(x => x.AuthorizeAsync(workspaceId, WorkspacePermission.ViewWorkspace,
            It.IsAny<CancellationToken>())).ReturnsAsync(Result<WorkspaceAuthorizationContext>.Success(
                new(workspaceId, Guid.NewGuid(), WorkspaceRole.Owner)));
        var handler = new StreamMessageQueryHandler(db, retrieval.Object, new FailingProvider(status),
            new CitationStreamStateMachine(), authorization.Object);
        var events = new List<ChatStreamEvent>();
        await foreach (var item in handler.Handle(new(workspaceId, null, "Question"), default)) events.Add(item);

        var terminal = events.Last();
        Assert.Equal(StreamEventType.Error, terminal.Type);
        Assert.Equal(ChatFailureMessages.ProviderUnavailable, terminal.Content);
        Assert.DoesNotContain(events, item => item.Type == StreamEventType.Done);
        Assert.DoesNotContain(events, item => item.Content?.Contains("private-provider-detail") == true);
        Assert.NotNull(terminal.ConversationId);
        Assert.NotNull(terminal.MessageId);
        Assert.Equal("Partial answer.", (await db.ChatMessages.SingleAsync(m => m.Role == MessageRole.Assistant)).Content);
        Assert.Equal(2, await db.ChatMessages.CountAsync());
    }

    private sealed class FailingProvider(int status) : IChatCompletionService
    {
        public Task<string> GenerateResponseAsync(IReadOnlyList<ChatPromptMessage> messages,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<string> StreamResponseAsync(IReadOnlyList<ChatPromptMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "Partial answer.";
            await Task.Yield();
            throw new HttpRequestException("private-provider-detail", null, (HttpStatusCode)status);
        }
    }
}
