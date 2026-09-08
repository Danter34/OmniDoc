using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Documents.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Documents.Queries.GetDocumentContent;

public sealed record GetDocumentContentQuery(
    Guid WorkspaceId,
    Guid DocumentId,
    bool Source = false) : IRequest<Result<DocumentFileStreamDto>>;

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
            WorkspacePermission.ManageDocuments,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<DocumentFileStreamDto>.Failure(
                access.Errors,
                access.StatusCode);
        }

        var document = await _context.Documents
            .AsNoTracking()
            .Include(item => item.Artifacts)
            .Where(item =>
                item.Id == request.DocumentId &&
                item.WorkspaceId == request.WorkspaceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return Result<DocumentFileStreamDto>.Failure(
                $"Document '{request.DocumentId}' was not found in workspace '{request.WorkspaceId}'.",
                404);
        }

        var artifactId = request.Source ? document.SourceArtifactId : document.CanonicalArtifactId;
        var kind = request.Source ? ArtifactKind.Source : ArtifactKind.CanonicalPdf;
        var artifact = document.Artifacts.SingleOrDefault(a => a.Id == artifactId && a.Kind == kind);
        // Legacy fallback is exclusively for PDF rows without artifact pointers.
        if (artifact is null && (artifactId is not null || document.DetectedFormat != DocumentFormat.Pdf))
            return Result<DocumentFileStreamDto>.Failure(request.Source ? "Source artifact is missing." : "Canonical PDF is not available yet.", request.Source ? 404 : 409);

        var stream = await _fileStorage.GetFileAsync(
            artifact?.StoragePath ?? document.StoragePath,
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
                request.Source ? artifact?.ContentType ?? document.ContentType : PdfContentType,
                request.Source ? Path.GetFileName(artifact?.FileName ?? document.FileName) : Path.ChangeExtension(Path.GetFileName(artifact?.FileName ?? document.FileName), ".pdf")));
    }
}
