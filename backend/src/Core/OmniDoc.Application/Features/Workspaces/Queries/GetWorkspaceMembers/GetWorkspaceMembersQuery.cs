using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Workspaces.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Workspaces.Queries.GetWorkspaceMembers;

public sealed record GetWorkspaceMembersQuery(Guid WorkspaceId)
    : IRequest<Result<List<WorkspaceMemberDto>>>;

public sealed class GetWorkspaceMembersQueryHandler
    : IRequestHandler<GetWorkspaceMembersQuery, Result<List<WorkspaceMemberDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public GetWorkspaceMembersQueryHandler(
        IApplicationDbContext context,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<List<WorkspaceMemberDto>>> Handle(
        GetWorkspaceMembersQuery request,
        CancellationToken cancellationToken)
    {
        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
            WorkspacePermission.ViewWorkspace,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<List<WorkspaceMemberDto>>.Failure(
                access.Errors,
                access.StatusCode);
        }

        var members = await _context.WorkspaceMembers
            .AsNoTracking()
            .Where(member => member.WorkspaceId == request.WorkspaceId)
            .OrderBy(member => member.Role)
            .ThenBy(member => member.User!.FullName)
            .Select(member => new WorkspaceMemberDto(
                member.UserId,
                member.User!.FullName,
                member.User.Email,
                member.Role.ToString(),
                member.JoinedAtUtc))
            .ToListAsync(cancellationToken);

        return Result<List<WorkspaceMemberDto>>.Success(members);
    }
}
