using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;

namespace OmniDoc.Application.Features.Chat.Commands.DeleteConversation;

public record DeleteConversationCommand(
    Guid WorkspaceId,
    Guid ConversationId) : IRequest<Result<bool>>;

public sealed class DeleteConversationCommandHandler
    : IRequestHandler<DeleteConversationCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public DeleteConversationCommandHandler(
        IApplicationDbContext context,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<bool>> Handle(
        DeleteConversationCommand request,
        CancellationToken cancellationToken)
    {
        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
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
