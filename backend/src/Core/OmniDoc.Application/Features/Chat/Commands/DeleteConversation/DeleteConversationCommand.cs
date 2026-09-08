using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Chat.Commands.DeleteConversation;

public record DeleteConversationCommand(
    Guid WorkspaceId,
    Guid ConversationId) : IRequest<Result<bool>>;

public sealed class DeleteConversationCommandHandler
    : IRequestHandler<DeleteConversationCommand, Result<bool>>
{
    private readonly IShowcasePolicy _showcase;
    private readonly IApplicationDbContext _context;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public DeleteConversationCommandHandler(
        IShowcasePolicy showcase,
        IApplicationDbContext context,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _showcase = showcase;
        _context = context;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<bool>> Handle(
        DeleteConversationCommand request,
        CancellationToken cancellationToken)
    {
        _showcase.EnsureCanDeleteConversation(request.WorkspaceId);

        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
            WorkspacePermission.ViewWorkspace,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<bool>.Failure(access.Errors, access.StatusCode);
        }

        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(
                item =>
                    item.Id == request.ConversationId &&
                    item.WorkspaceId == request.WorkspaceId,
                cancellationToken);

        if (conversation is null)
        {
            return Result<bool>.Failure(
                $"Conversation '{request.ConversationId}' was not found.",
                404);
        }

        _context.Conversations.Remove(conversation);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
