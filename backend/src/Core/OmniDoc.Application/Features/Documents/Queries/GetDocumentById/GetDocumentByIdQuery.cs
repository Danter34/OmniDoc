using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Documents.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Documents.Queries.GetDocumentById;

public record GetDocumentByIdQuery(Guid DocumentId) : IRequest<Result<DocumentDto>>;

public class GetDocumentByIdQueryHandler : IRequestHandler<GetDocumentByIdQuery, Result<DocumentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public GetDocumentByIdQueryHandler(
        IApplicationDbContext context,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<DocumentDto>> Handle(GetDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result<DocumentDto>.Failure(
                $"Document '{request.DocumentId}' was not found.",
                404);
        }

        var access = await _workspaceAuthorization.AuthorizeAsync(
            document.WorkspaceId,
            WorkspacePermission.ManageDocuments,
            cancellationToken);

        return access.IsSuccess
            ? Result<DocumentDto>.Success(document.ToDto())
            : Result<DocumentDto>.Failure(access.Errors, access.StatusCode);
    }
}
