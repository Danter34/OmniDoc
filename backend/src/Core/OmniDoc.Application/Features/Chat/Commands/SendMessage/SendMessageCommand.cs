using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Chat.DTOs;
using OmniDoc.Application.Features.Retrieval.DTOs;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Chat.Commands.SendMessage;

public record SendMessageCommand(
    Guid WorkspaceId,
    Guid? ConversationId,
    string Message,
    int TopK = 4) : IRequest<Result<ChatResponseDto>>;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.TopK).InclusiveBetween(1, 20);
    }
}

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, Result<ChatResponseDto>>
{
    private const int TitleLength = 30;
    private const int HistoryMessageLimit = 10;
    private const int ExcerptLength = 400;
    private const float MinSimilarityScore = 0.0f;

    private readonly IApplicationDbContext _context;
    private readonly IRetrievalService _retrievalService;
    private readonly IChatCompletionService _chatCompletion;

    public SendMessageCommandHandler(
        IApplicationDbContext context,
        IRetrievalService retrievalService,
        IChatCompletionService chatCompletion)
    {
        _context = context;
        _retrievalService = retrievalService;
        _chatCompletion = chatCompletion;
    }

    public async Task<Result<ChatResponseDto>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var workspaceExists = await _context.Workspaces
            .AnyAsync(w => w.Id == request.WorkspaceId, cancellationToken);

        if (!workspaceExists)
        {
            return Result<ChatResponseDto>.Failure($"Workspace '{request.WorkspaceId}' was not found.", 404);
        }

        Conversation conversation;

        if (request.ConversationId is { } conversationId)
        {
            var existing = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

            if (existing is null)
            {
                return Result<ChatResponseDto>.Failure($"Conversation '{conversationId}' was not found.", 404);
            }

            if (existing.WorkspaceId != request.WorkspaceId)
            {
                return Result<ChatResponseDto>.Failure(
                    $"Conversation '{conversationId}' does not belong to workspace '{request.WorkspaceId}'.", 403);
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

        var history = await LoadRecentHistoryAsync(conversation.Id, cancellationToken);

        var matches = await _retrievalService.SearchSimilarChunksAsync(
            request.WorkspaceId,
            request.Message,
            request.TopK,
            MinSimilarityScore,
            cancellationToken);

        var prompt = new List<ChatPromptMessage>
        {
            new(nameof(MessageRole.System), BuildSystemPrompt(matches))
        };

        prompt.AddRange(history);
        prompt.Add(new ChatPromptMessage(nameof(MessageRole.User), request.Message));

        var answer = await _chatCompletion.GenerateResponseAsync(prompt, cancellationToken);

        var userMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = MessageRole.User,
            Content = request.Message
        };

        var assistantMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = MessageRole.Assistant,
            Content = answer,
            Citations = matches.Select(match => new Citation
            {
                ChunkId = match.ChunkId,
                DocumentId = match.DocumentId,
                DocumentTitle = match.DocumentTitle,
                PageNumber = match.PageNumber,
                Excerpt = Truncate(match.Content, ExcerptLength),
                SimilarityScore = match.SimilarityScore
            }).ToList()
        };

        // The assistant reply must sort after the question even when both saves land
        // inside the same clock tick, since history is ordered by CreatedAtUtc. The
        // offset has to exceed Postgres' microsecond precision to survive the round-trip.
        assistantMessage.CreatedAtUtc = userMessage.CreatedAtUtc.AddMilliseconds(1);

        _context.ChatMessages.AddRange(userMessage, assistantMessage);

        await _context.SaveChangesAsync(cancellationToken);

        var response = new ChatResponseDto(
            conversation.Id,
            userMessage.ToDto(),
            assistantMessage.ToDto());

        return Result<ChatResponseDto>.Success(response);
    }

    private async Task<List<ChatPromptMessage>> LoadRecentHistoryAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var recent = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Role != MessageRole.System)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(HistoryMessageLimit)
            .Select(m => new { m.Role, m.Content, m.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        return recent
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new ChatPromptMessage(m.Role.ToString(), m.Content))
            .ToList();
    }

    private static string BuildSystemPrompt(IReadOnlyList<SearchResultDto> matches)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Bạn là trợ lý AI OmniDoc. Hãy trả lời câu hỏi của người dùng CHỈ dựa trên các tài liệu được cung cấp dưới đây.");
        builder.AppendLine("Nếu không tìm thấy thông tin trong tài liệu, hãy nói rõ rằng bạn không biết.");
        builder.AppendLine("---");
        builder.AppendLine("NGỮ CẢNH TÀI LIỆU:");

        if (matches.Count == 0)
        {
            builder.AppendLine("(Không tìm thấy tài liệu liên quan trong workspace này.)");
            return builder.ToString();
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            builder.AppendLine($"[Nguồn {i + 1}: {match.DocumentTitle} - Trang {match.PageNumber}]");
            builder.AppendLine(match.Content);
            builder.AppendLine();
        }

        return builder.ToString();
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
}
