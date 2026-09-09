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
        RuleFor(x => x.Name).NotEmpty().WithMessage("{PropertyName} không được để trống.").MaximumLength(256).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Tên không gian làm việc");
    }
}

public class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, Result<WorkspaceDto>>
{
    private readonly IShowcasePolicy _showcase;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateWorkspaceCommandHandler(
        IShowcasePolicy showcase,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _showcase = showcase;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<WorkspaceDto>> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<WorkspaceDto>.Failure("Bạn không có quyền thực hiện thao tác này.", 401);
        }

        _showcase.EnsureCanModifyAccount(userId);

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

        var dto = new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.CreatedAtUtc,
            0,
            WorkspaceRole.Owner.ToString());

        return Result<WorkspaceDto>.Success(dto, 201);
    }
}
