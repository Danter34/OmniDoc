using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Documents.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Documents.Queries.GetDocumentsByWorkspace;

public record GetDocumentsByWorkspaceQuery(Guid WorkspaceId) : IRequest<Result<List<DocumentDto>>>;

public class GetDocumentsByWorkspaceQueryHandler : IRequestHandler<GetDocumentsByWorkspaceQuery, Result<List<DocumentDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public GetDocumentsByWorkspaceQueryHandler(
        IApplicationDbContext context,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<List<DocumentDto>>> Handle(GetDocumentsByWorkspaceQuery request, CancellationToken cancellationToken)
    {
        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
            WorkspacePermission.ManageDocuments,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<List<DocumentDto>>.Failure(access.Errors, access.StatusCode);
        }

        var documents = await _context.Documents
            .AsNoTracking()
            .Where(d => d.WorkspaceId == request.WorkspaceId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(DocumentMapping.Projection)
            .ToListAsync(cancellationToken);

        return Result<List<DocumentDto>>.Success(documents);
    }
}
