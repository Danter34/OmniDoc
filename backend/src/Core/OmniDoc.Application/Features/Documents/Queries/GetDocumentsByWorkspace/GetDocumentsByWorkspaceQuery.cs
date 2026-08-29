using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Documents.DTOs;

namespace OmniDoc.Application.Features.Documents.Queries.GetDocumentsByWorkspace;

public record GetDocumentsByWorkspaceQuery(Guid WorkspaceId) : IRequest<Result<List<DocumentDto>>>;

public class GetDocumentsByWorkspaceQueryHandler : IRequestHandler<GetDocumentsByWorkspaceQuery, Result<List<DocumentDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetDocumentsByWorkspaceQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DocumentDto>>> Handle(GetDocumentsByWorkspaceQuery request, CancellationToken cancellationToken)
    {
        var documents = await _context.Documents
            .AsNoTracking()
            .Where(d => d.WorkspaceId == request.WorkspaceId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(DocumentMapping.Projection)
            .ToListAsync(cancellationToken);

        return Result<List<DocumentDto>>.Success(documents);
    }
}
