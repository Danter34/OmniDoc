using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Documents.DTOs;

namespace OmniDoc.Application.Features.Documents.Queries.GetDocumentContent;

public sealed record GetDocumentContentQuery(
    Guid WorkspaceId,
    Guid DocumentId) : IRequest<Result<DocumentFileStreamDto>>;

public sealed class GetDocumentContentQueryHandler
    : IRequestHandler<GetDocumentContentQuery, Result<DocumentFileStreamDto>>
{
    private const string PdfContentType = "application/pdf";

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public GetDocumentContentQueryHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorage,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _fileStorage = fileStorage;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<DocumentFileStreamDto>> Handle(
        GetDocumentContentQuery request,
        CancellationToken cancellationToken)
    {
        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<DocumentFileStreamDto>.Failure(
                access.Errors,
                access.StatusCode);
        }

        var document = await _context.Documents
            .AsNoTracking()
            .Where(item =>
                item.Id == request.DocumentId &&
                item.WorkspaceId == request.WorkspaceId)
            .Select(item => new
            {
                item.FileName,
                item.StoragePath
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return Result<DocumentFileStreamDto>.Failure(
                $"Document '{request.DocumentId}' was not found in workspace '{request.WorkspaceId}'.",
                404);
        }

        var stream = await _fileStorage.GetFileAsync(
            document.StoragePath,
            cancellationToken);

        if (stream is null)
        {
            return Result<DocumentFileStreamDto>.Failure(
                $"The content for document '{request.DocumentId}' was not found.",
                404);
        }

        return Result<DocumentFileStreamDto>.Success(
            new DocumentFileStreamDto(
                stream,
                PdfContentType,
                Path.GetFileName(document.FileName)));
    }
}
