using FluentValidation;
using MediatR;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Workspaces.DTOs;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Workspaces.Commands.CreateWorkspace;

public record CreateWorkspaceCommand(string Name, string? Description, string? UserId) : IRequest<Result<WorkspaceDto>>;

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

    public CreateWorkspaceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<WorkspaceDto>> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var userId = request.UserId ?? "system-user";

        var workspace = new Workspace
        {
            Name = request.Name,
            Description = request.Description,
            CreatedBy = userId
        };

        workspace.Members.Add(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = userId,
            Role = WorkspaceRole.Owner,
            CreatedBy = userId
        });

        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new WorkspaceDto(workspace.Id, workspace.Name, workspace.Description, workspace.CreatedAtUtc, 0);

        return Result<WorkspaceDto>.Success(dto, 201);
    }
}
