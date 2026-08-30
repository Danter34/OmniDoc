using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Chat.DTOs;

namespace OmniDoc.Application.Features.Chat.Queries.GetConversationsByWorkspace;

public record GetConversationsByWorkspaceQuery(Guid WorkspaceId) : IRequest<Result<List<ConversationDto>>>;

public class GetConversationsByWorkspaceQueryHandler
    : IRequestHandler<GetConversationsByWorkspaceQuery, Result<List<ConversationDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetConversationsByWorkspaceQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ConversationDto>>> Handle(
        GetConversationsByWorkspaceQuery request,
        CancellationToken cancellationToken)
    {
        var workspaceExists = await _context.Workspaces
            .AnyAsync(w => w.Id == request.WorkspaceId, cancellationToken);

        if (!workspaceExists)
        {
            return Result<List<ConversationDto>>.Failure($"Workspace '{request.WorkspaceId}' was not found.", 404);
        }

        var conversations = await _context.Conversations
            .AsNoTracking()
            .Where(c => c.WorkspaceId == request.WorkspaceId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(ChatMapping.ConversationProjection)
            .ToListAsync(cancellationToken);

        return Result<List<ConversationDto>>.Success(conversations);
    }
}
