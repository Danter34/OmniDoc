using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Workspaces.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Workspaces.Queries.GetWorkspaces;

public record GetWorkspacesQuery : IRequest<Result<List<WorkspaceDto>>>;

public class GetWorkspacesQueryHandler : IRequestHandler<GetWorkspacesQuery, Result<List<WorkspaceDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetWorkspacesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<WorkspaceDto>>> Handle(GetWorkspacesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<List<WorkspaceDto>>.Failure("Authentication is required.", 401);
        }

        var workspaceRows = await _context.Workspaces
            .AsNoTracking()
            .Where(workspace =>
                workspace.OwnerId == userId ||
                workspace.Members.Any(member => member.UserId == userId))
            .OrderByDescending(w => w.CreatedAtUtc)
            .Select(workspace => new
            {
                workspace.Id,
                workspace.Name,
                workspace.Description,
                workspace.CreatedAtUtc,
                DocumentCount = workspace.Documents.Count,
                Role = workspace.OwnerId == userId
                    ? WorkspaceRole.Owner
                    : workspace.Members
                        .Where(member => member.UserId == userId)
                        .Select(member => member.Role)
                        .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var workspaces = workspaceRows
            .Select(workspace => new WorkspaceDto(
                workspace.Id,
                workspace.Name,
                workspace.Description,
                workspace.CreatedAtUtc,
                workspace.DocumentCount,
                workspace.Role.ToString()))
            .ToList();

        return Result<List<WorkspaceDto>>.Success(workspaces);
    }
}
