using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Workspaces.DTOs;

namespace OmniDoc.Application.Features.Workspaces.Queries.GetWorkspaces;

public record GetWorkspacesQuery(string? UserId = null) : IRequest<Result<List<WorkspaceDto>>>;

public class GetWorkspacesQueryHandler : IRequestHandler<GetWorkspacesQuery, Result<List<WorkspaceDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetWorkspacesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<WorkspaceDto>>> Handle(GetWorkspacesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Workspaces.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            query = query.Where(w => w.Members.Any(m => m.UserId == request.UserId));
        }

        var workspaces = await query
            .OrderByDescending(w => w.CreatedAtUtc)
            .Select(w => new WorkspaceDto(w.Id, w.Name, w.Description, w.CreatedAtUtc, w.Documents.Count))
            .ToListAsync(cancellationToken);

        return Result<List<WorkspaceDto>>.Success(workspaces);
    }
}
