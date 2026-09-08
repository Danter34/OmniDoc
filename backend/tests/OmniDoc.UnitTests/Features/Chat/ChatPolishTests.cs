using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Chat.Commands.SendMessage;
using OmniDoc.Application.Features.Chat.DTOs;
using OmniDoc.Application.Features.Chat.Services;
using OmniDoc.Application.Features.Chat.Streaming.StreamMessage;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Services;
using OmniDoc.UnitTests.Features.Documents;

namespace OmniDoc.UnitTests.Features.Chat;

public sealed class ChatPolishTests
{
    [Theory]
    [InlineData("  Xin\r\n chào\t bạn  ", "Xin chào bạn")]
    [InlineData("một hai ba bốn năm sáu bảy tám chín", "một hai ba bốn năm sáu bảy tám...")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJK...")]
    [InlineData("", "Cuộc trò chuyện mới")]
    public void Title_NormalizesAndLimitsWordsAndCharacters(string message, string expected)
    {
        var title = ConversationNaming.BuildTitle(message);
        Assert.Equal(expected, title);
        Assert.True(title.Length <= 40);
        Assert.True(title.Split(' ').Length <= 8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothChatPaths_NamePrecreatedSessionAndAnswerWithoutSources(bool streaming)
    {
        await using var context = new TestApplicationDbContext();
        var conversation = new Conversation { WorkspaceId = Guid.NewGuid(), Title = "Cuộc trò chuyện mới" };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();
        var retrieval = new Mock<IRetrievalService>();
        retrieval.Setup(service => service.SearchSimilarChunksAsync(conversation.WorkspaceId,
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var completion = new RecordingCompletion();
        var authorization = new Mock<IWorkspaceAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(conversation.WorkspaceId,
            WorkspacePermission.ViewWorkspace, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<WorkspaceAuthorizationContext>.Success(
                new(conversation.WorkspaceId, Guid.NewGuid(), WorkspaceRole.Owner)));

        const string message = "  Xin\n chào bạn ";
        if (streaming)
        {
            var handler = new StreamMessageQueryHandler(context, retrieval.Object, completion,
                new CitationStreamStateMachine(), authorization.Object);
            var events = new List<ChatStreamEvent>();
            await foreach (var item in handler.Handle(new(conversation.WorkspaceId, conversation.Id, message), default))
                events.Add(item);
            Assert.Equal("Xin chào bạn", events[0].ConversationTitle);
            Assert.Equal(conversation.Id, events[0].ConversationId);
            Assert.Equal(StreamEventType.Done, events[^1].Type);
            Assert.DoesNotContain(events, item => item.Type is StreamEventType.Error or StreamEventType.Citation);
        }
        else
        {
            var handler = new SendMessageCommandHandler(context, retrieval.Object, completion, authorization.Object);
            var response = await handler.Handle(new(conversation.WorkspaceId, conversation.Id, message), default);
            Assert.True(response.IsSuccess);
            Assert.Equal("Xin chào bạn", response.Data!.ConversationTitle);
            Assert.Empty(response.Data.AssistantMessage.Citations);
        }

        Assert.Contains("kiến thức tổng quát", completion.Prompt![0].Content);
        Assert.Contains(RagPromptBuilder.EmptyWorkspaceReminder, completion.Prompt[0].Content);
        context.ChangeTracker.Clear();
        Assert.Equal("Xin chào bạn", (await context.Conversations.SingleAsync()).Title);
        Assert.Equal(2, await context.ChatMessages.CountAsync());
    }

    [Theory]
    [InlineData("Cuộc trò chuyện mới", "Câu hỏi đầu tiên")]
    [InlineData("Tên đã đặt", "Tên đã đặt")]
    public async Task Naming_UsesOriginalMessageForDefaultTitleAndPreservesExistingTitle(string title, string expected)
    {
        await using var context = new TestApplicationDbContext();
        var conversation = new Conversation { WorkspaceId = Guid.NewGuid(), Title = title };
        context.Conversations.Add(conversation);
        context.ChatMessages.Add(new ChatMessage { ConversationId = conversation.Id, Role = MessageRole.User, Content = "Câu hỏi đầu tiên" });
        await context.SaveChangesAsync();
        await ConversationNaming.UpdateAsync(context, conversation, "Câu hỏi tiếp theo", default);
        Assert.Equal(expected, conversation.Title);
    }

    [Fact]
    public async Task EmptyRetrieval_DoesNotCallEmbeddingProvider()
    {
        await using var context = new TestApplicationDbContext();
        var embeddings = new Mock<IEmbeddingService>(MockBehavior.Strict);
        var retrieval = new VectorRetrievalService(context, embeddings.Object, NullLogger<VectorRetrievalService>.Instance);
        Assert.Empty(await retrieval.SearchSimilarChunksAsync(Guid.NewGuid(), "Xin chào"));
        embeddings.VerifyNoOtherCalls();
    }

    [Fact]
    public void NoMatchesInExistingWorkspace_DoesNotClaimWorkspaceIsEmpty()
    {
        var prompt = RagPromptBuilder.BuildSystemPrompt([], hasDocuments: true);
        Assert.Contains(RagPromptBuilder.NoMatchesReminder, prompt);
        Assert.DoesNotContain(RagPromptBuilder.EmptyWorkspaceReminder, prompt);
        Assert.Contains("Không bịa nguồn", prompt);
    }

    private sealed class RecordingCompletion : IChatCompletionService
    {
        public IReadOnlyList<ChatPromptMessage>? Prompt { get; private set; }
        public Task<string> GenerateResponseAsync(IReadOnlyList<ChatPromptMessage> messages, CancellationToken cancellationToken = default)
        {
            Prompt = messages;
            return Task.FromResult("Xin chào! Tôi có thể giúp bạn tìm hiểu kiến thức tổng quát.");
        }
        public async IAsyncEnumerable<string> StreamResponseAsync(IReadOnlyList<ChatPromptMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await GenerateResponseAsync(messages, cancellationToken);
        }
    }
}
