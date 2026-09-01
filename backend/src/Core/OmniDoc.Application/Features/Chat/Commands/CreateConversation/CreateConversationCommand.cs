using FluentValidation;
using MediatR;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Chat.DTOs;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Application.Features.Chat.Commands.CreateConversation;

public record CreateConversationCommand(
    Guid WorkspaceId,
    string Title) : IRequest<Result<ConversationDto>>;

public sealed class CreateConversationCommandValidator
    : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator()
    {
        RuleFor(command => command.WorkspaceId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(512);
    }
}

public sealed class CreateConversationCommandHandler
    : IRequestHandler<CreateConversationCommand, Result<ConversationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public CreateConversationCommandHandler(
        IApplicationDbContext context,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<ConversationDto>> Handle(
        CreateConversationCommand request,
        CancellationToken cancellationToken)
    {
        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<ConversationDto>.Failure(
                access.Errors,
                access.StatusCode);
        }

        var conversation = new Conversation
        {
            WorkspaceId = request.WorkspaceId,
            Title = request.Title.Trim()
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ConversationDto>.Success(
            new ConversationDto(
                conversation.Id,
                conversation.WorkspaceId,
                conversation.Title,
                conversation.CreatedAtUtc,
                conversation.CreatedAtUtc),
            201);
    }
}
