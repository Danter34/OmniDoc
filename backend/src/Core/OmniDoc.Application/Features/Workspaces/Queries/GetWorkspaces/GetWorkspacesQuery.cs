using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Workspaces.DTOs;

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

        var workspaces = await _context.Workspaces
            .AsNoTracking()
            .Where(workspace =>
                workspace.OwnerId == userId ||
                workspace.Members.Any(member => member.UserId == userId))
            .OrderByDescending(w => w.CreatedAtUtc)
            .Select(w => new WorkspaceDto(w.Id, w.Name, w.Description, w.CreatedAtUtc, w.Documents.Count))
            .ToListAsync(cancellationToken);

        return Result<List<WorkspaceDto>>.Success(workspaces);
    }
}
