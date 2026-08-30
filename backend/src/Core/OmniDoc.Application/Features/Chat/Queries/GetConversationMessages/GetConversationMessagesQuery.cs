using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Chat.DTOs;

namespace OmniDoc.Application.Features.Chat.Queries.GetConversationMessages;

public record GetConversationMessagesQuery(Guid ConversationId) : IRequest<Result<List<ChatMessageDto>>>;

public class GetConversationMessagesQueryHandler
    : IRequestHandler<GetConversationMessagesQuery, Result<List<ChatMessageDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetConversationMessagesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ChatMessageDto>>> Handle(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var conversationExists = await _context.Conversations
            .AnyAsync(c => c.Id == request.ConversationId, cancellationToken);

        if (!conversationExists)
        {
            return Result<List<ChatMessageDto>>.Failure($"Conversation '{request.ConversationId}' was not found.", 404);
        }

        var messages = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(ChatMapping.MessageProjection)
            .ToListAsync(cancellationToken);

        return Result<List<ChatMessageDto>>.Success(messages);
    }
}
