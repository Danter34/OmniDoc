using System.Runtime.CompilerServices;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Features.Chat.DTOs;
using OmniDoc.Application.Features.Chat.Services;
using OmniDoc.Application.Features.Retrieval.DTOs;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Chat.Streaming.StreamMessage;

public record StreamMessageQuery(
    Guid WorkspaceId,
    Guid? ConversationId,
    string Message,
    int TopK = 4) : IStreamRequest<ChatStreamEvent>;

/// Streaming counterpart of SendMessageCommand. Emits token/citation frames as the model
/// produces them and persists the assembled answer once the stream settles.
///
/// Failures surface as a terminal <see cref="StreamEventType.Error"/> frame rather than an
/// exception, because by the time the first token is written the response has already been
/// committed with a 200 and SSE has no way to revise the status code.
public class StreamMessageQueryHandler : IStreamRequestHandler<StreamMessageQuery, ChatStreamEvent>
{
    private const int TitleLength = 30;
    private const int ExcerptLength = 400;
    private const int MaxMessageLength = 4000;
    private const float MinSimilarityScore = 0.0f;

    private readonly IApplicationDbContext _context;
    private readonly IRetrievalService _retrievalService;
    private readonly IChatCompletionService _chatCompletion;
    private readonly CitationStreamStateMachine _stateMachine;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public StreamMessageQueryHandler(
        IApplicationDbContext context,
        IRetrievalService retrievalService,
        IChatCompletionService chatCompletion,
        CitationStreamStateMachine stateMachine,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _retrievalService = retrievalService;
        _chatCompletion = chatCompletion;
        _stateMachine = stateMachine;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async IAsyncEnumerable<ChatStreamEvent> Handle(
        StreamMessageQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var setup = await PrepareAsync(request, cancellationToken).ConfigureAwait(false);

        if (setup.Error is { } setupError)
        {
            yield return new ChatStreamEvent(StreamEventType.Error, setupError);
            yield break;
        }

        var conversation = setup.Conversation!;
        var matches = setup.Matches!;

        yield return new ChatStreamEvent(
            StreamEventType.Token,
            string.Empty,
            ConversationId: conversation.Id);

        var prompt = RagPromptBuilder.BuildPrompt(matches, setup.History!, request.Message);

        var answer = new StringBuilder();
        var citations = new List<CitationDto>();
        var failure = new StreamFailure();
        ChatMessage? assistantMessage = null;

        // Persistence lives in the finally so a client disconnect mid-stream still records
        // the partial answer instead of dropping the turn entirely.
        try
        {
            var tokens = ReadTokensAsync(_chatCompletion.StreamResponseAsync(prompt, cancellationToken), failure, cancellationToken);

            await foreach (var streamEvent in _stateMachine
                .ProcessStreamAsync(tokens, matches, cancellationToken)
                .ConfigureAwait(false))
            {
                if (streamEvent.Type == StreamEventType.Token)
                {
                    if (string.IsNullOrEmpty(streamEvent.Content))
                    {
                        continue;
                    }

                    answer.Append(streamEvent.Content);
                }
                else if (streamEvent.Type == StreamEventType.Citation && streamEvent.Citation is { } citation)
                {
                    if (citations.Any(existing => existing.ChunkId == citation.ChunkId))
                    {
                        // Repeated reference to a chunk already cited: still worth telling the
                        // client so it can highlight the marker, but persist it only once.
                        yield return streamEvent;
                        continue;
                    }

                    citations.Add(citation);
                }

                yield return streamEvent;
            }
        }
        finally
        {
            assistantMessage = await PersistAssistantMessageAsync(
                conversation.Id,
                setup.UserMessageCreatedAtUtc,
                answer.ToString(),
                citations).ConfigureAwait(false);
        }

        if (failure.Exception is { } streamException)
        {
            yield return new ChatStreamEvent(
                StreamEventType.Error,
                $"Việc sinh câu trả lời bị lỗi giữa luồng: {streamException.Message}",
                ConversationId: conversation.Id,
                MessageId: assistantMessage?.Id);

            yield break;
        }

        yield return new ChatStreamEvent(
            StreamEventType.Done,
            ConversationId: conversation.Id,
            MessageId: assistantMessage?.Id);
    }

    /// Resolves the workspace and conversation, records the user turn, and retrieves
    /// context. Runs before the first frame is written so problems can still be reported
    /// as a plain error frame with nothing half-streamed.
    private async Task<StreamSetup> PrepareAsync(StreamMessageQuery request, CancellationToken cancellationToken)
    {
        if (request.WorkspaceId == Guid.Empty)
        {
            return StreamSetup.Failed("WorkspaceId không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return StreamSetup.Failed("Message không được để trống.");
        }

        if (request.Message.Length > MaxMessageLength)
        {
            return StreamSetup.Failed($"Message không được dài hơn {MaxMessageLength} ký tự.");
        }

        if (request.TopK is < 1 or > 20)
        {
            return StreamSetup.Failed("TopK phải nằm trong khoảng từ 1 đến 20.");
        }

        var access = await _workspaceAuthorization
            .AuthorizeAsync(
                request.WorkspaceId,
                WorkspacePermission.ViewWorkspace,
                cancellationToken)
            .ConfigureAwait(false);

        if (!access.IsSuccess)
        {
            return StreamSetup.Failed(access.Error ?? "Workspace access was denied.");
        }

        Conversation conversation;

        if (request.ConversationId is { } conversationId)
        {
            var existing = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                return StreamSetup.Failed($"Conversation '{conversationId}' was not found.");
            }

            if (existing.WorkspaceId != request.WorkspaceId)
            {
                return StreamSetup.Failed(
                    $"Conversation '{conversationId}' does not belong to workspace '{request.WorkspaceId}'.");
            }

            conversation = existing;
        }
        else
        {
            conversation = new Conversation
            {
                WorkspaceId = request.WorkspaceId,
                Title = BuildTitle(request.Message)
            };

            _context.Conversations.Add(conversation);
        }

        var history = await _context
            .LoadRecentHistoryAsync(conversation.Id, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var userMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = MessageRole.User,
            Content = request.Message
        };

        conversation.UpdatedAtUtc = userMessage.CreatedAtUtc;
        _context.ChatMessages.Add(userMessage);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var matches = await _retrievalService.SearchSimilarChunksAsync(
            request.WorkspaceId,
            request.Message,
            request.TopK,
            MinSimilarityScore,
            cancellationToken).ConfigureAwait(false);

        return new StreamSetup
        {
            Conversation = conversation,
            History = history,
            Matches = matches,
            UserMessageCreatedAtUtc = userMessage.CreatedAtUtc
        };
    }

    private async Task<ChatMessage?> PersistAssistantMessageAsync(
        Guid conversationId,
        DateTime userMessageCreatedAtUtc,
        string answer,
        IReadOnlyList<CitationDto> citations)
    {
        if (answer.Length == 0 && citations.Count == 0)
        {
            return null;
        }

        var assistantMessage = new ChatMessage
        {
            ConversationId = conversationId,
            Role = MessageRole.Assistant,
            Content = answer,
            Citations = citations.Select(citation => new Citation
            {
                ChunkId = citation.ChunkId,
                DocumentId = citation.DocumentId,
                DocumentTitle = citation.DocumentName,
                PageNumber = citation.PageNumber,
                Excerpt = Truncate(citation.Snippet, ExcerptLength),
                SimilarityScore = citation.SimilarityScore
            }).ToList()
        };

        // History is ordered by CreatedAtUtc, and the offset has to exceed Postgres'
        // microsecond precision to survive the round-trip when both rows land in one tick.
        if (assistantMessage.CreatedAtUtc <= userMessageCreatedAtUtc)
        {
            assistantMessage.CreatedAtUtc = userMessageCreatedAtUtc.AddMilliseconds(1);
        }

        _context.ChatMessages.Add(assistantMessage);

        // CancellationToken.None on purpose: this runs on the disconnect path too, where the
        // request token is already cancelled and would abort the write we are trying to make.
        await _context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        return assistantMessage;
    }

    /// Forwards provider tokens while trapping provider faults. Manual enumeration keeps the
    /// yield outside the try block, which the compiler requires for a catch clause here.
    private static async IAsyncEnumerable<string> ReadTokensAsync(
        IAsyncEnumerable<string> source,
        StreamFailure failure,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            string current;

            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                current = enumerator.Current;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failure.Exception = exception;
                break;
            }

            yield return current;
        }
    }

    private static string BuildTitle(string message)
    {
        var normalized = message.Trim();

        return normalized.Length <= TitleLength
            ? normalized
            : normalized[..TitleLength].TrimEnd() + "...";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "...";

    private sealed class StreamFailure
    {
        public Exception? Exception { get; set; }
    }

    private sealed class StreamSetup
    {
        public string? Error { get; init; }

        public Conversation? Conversation { get; init; }

        public List<ChatPromptMessage>? History { get; init; }

        public IReadOnlyList<SearchResultDto>? Matches { get; init; }

        public DateTime UserMessageCreatedAtUtc { get; init; }

        public static StreamSetup Failed(string error) => new() { Error = error };
    }
}
