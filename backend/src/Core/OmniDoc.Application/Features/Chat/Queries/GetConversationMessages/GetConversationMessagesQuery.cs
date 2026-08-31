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
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public GetConversationMessagesQueryHandler(
        IApplicationDbContext context,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<List<ChatMessageDto>>> Handle(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var conversation = await _context.Conversations
            .AsNoTracking()
            .Where(item => item.Id == request.ConversationId)
            .Select(item => new { item.WorkspaceId })
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return Result<List<ChatMessageDto>>.Failure($"Conversation '{request.ConversationId}' was not found.", 404);
        }

        var access = await _workspaceAuthorization.AuthorizeAsync(
            conversation.WorkspaceId,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<List<ChatMessageDto>>.Failure(access.Errors, access.StatusCode);
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
