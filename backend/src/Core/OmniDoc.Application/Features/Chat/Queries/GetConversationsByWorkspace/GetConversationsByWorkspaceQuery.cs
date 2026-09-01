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
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public GetConversationsByWorkspaceQueryHandler(
        IApplicationDbContext context,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<List<ConversationDto>>> Handle(
        GetConversationsByWorkspaceQuery request,
        CancellationToken cancellationToken)
    {
        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<List<ConversationDto>>.Failure(access.Errors, access.StatusCode);
        }

        var conversations = await _context.Conversations
            .AsNoTracking()
            .Where(c => c.WorkspaceId == request.WorkspaceId)
            .OrderByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
            .Select(ChatMapping.ConversationProjection)
            .ToListAsync(cancellationToken);

        return Result<List<ConversationDto>>.Success(conversations);
    }
}
