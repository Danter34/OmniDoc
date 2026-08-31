using FluentValidation;
using MediatR;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Workspaces.DTOs;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Workspaces.Commands.CreateWorkspace;

public record CreateWorkspaceCommand(
    string Name,
    string? Description) : IRequest<Result<WorkspaceDto>>;

public class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}

public class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, Result<WorkspaceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateWorkspaceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<WorkspaceDto>> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<WorkspaceDto>.Failure("Authentication is required.", 401);
        }

        var workspace = new Workspace
        {
            Name = request.Name,
            Description = request.Description,
            OwnerId = userId,
            CreatedBy = userId.ToString()
        };

        workspace.Members.Add(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = userId,
            Role = WorkspaceRole.Owner
        });

        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new WorkspaceDto(workspace.Id, workspace.Name, workspace.Description, workspace.CreatedAtUtc, 0);

        return Result<WorkspaceDto>.Success(dto, 201);
    }
}
