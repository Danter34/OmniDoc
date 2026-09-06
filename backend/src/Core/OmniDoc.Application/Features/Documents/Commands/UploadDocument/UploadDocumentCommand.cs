using FluentValidation;
using Hangfire;
using MediatR;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Documents.DTOs;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Domain.Exceptions;

namespace OmniDoc.Application.Features.Documents.Commands.UploadDocument;

public record UploadDocumentCommand(
    Guid WorkspaceId,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? Title = null) : IRequest<Result<DocumentDto>>;

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public const long MaxFileSizeBytes = 50L * 1024 * 1024;

    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();

        RuleFor(x => x.FileName)
            .NotEmpty()
            .Must(name => new[] { ".pdf", ".txt", ".md", ".markdown", ".docx", ".pptx", ".csv", ".xlsx" }.Contains(Path.GetExtension(name).ToLowerInvariant()))
            .WithMessage("Only PDF, TXT, Markdown, DOCX, PPTX, CSV and XLSX files are supported.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB.");
    }
}

public class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, Result<DocumentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDocumentArtifactStorage _artifactStorage;
    private readonly IDocumentFormatDetector _detector;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public UploadDocumentCommandHandler(
        IApplicationDbContext context,
        IDocumentArtifactStorage artifactStorage,
        IDocumentFormatDetector detector,
        IBackgroundJobClient backgroundJobClient,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _artifactStorage = artifactStorage;
        _detector = detector;
        _backgroundJobClient = backgroundJobClient;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<DocumentDto>> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
            WorkspacePermission.ManageDocuments,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<DocumentDto>.Failure(access.Errors, access.StatusCode);
        }

        DetectedDocumentFormat detected;
        try { detected = await _detector.DetectAsync(request.FileStream, request.FileName, cancellationToken); }
        catch (DocumentProcessingException ex) { return Result<DocumentDto>.Failure(ex.Message, 400, ex.Code.ToString()); }
        catch (InvalidDataException ex) { return Result<DocumentDto>.Failure(ex.Message, 400); }

        var document = new Document
        {
            WorkspaceId = request.WorkspaceId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? Path.GetFileNameWithoutExtension(request.FileName) : request.Title,
            FileName = Path.GetFileName(request.FileName),
            ContentType = detected.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            DetectedFormat = detected.Format,
            Status = DocumentStatus.Pending
        };

        var source = await _artifactStorage.SaveAsync(request.FileStream, request.WorkspaceId, document.Id,
            ArtifactKind.Source, request.FileName, detected.ContentType, "Upload", cancellationToken);
        document.AddArtifact(source);
        document.StoragePath = source.StoragePath;
        document.FileSizeBytes = source.FileSizeBytes;
        if (detected.Format == DocumentFormat.Pdf)
            document.AddArtifact(new DocumentArtifact
            {
                DocumentId = document.Id, Kind = ArtifactKind.CanonicalPdf, FileName = Path.ChangeExtension(source.FileName, ".pdf"),
                ContentType = "application/pdf", FileSizeBytes = source.FileSizeBytes, StoragePath = source.StoragePath,
                Sha256 = source.Sha256, Producer = "PassThrough"
            });

        _context.Documents.Add(document);
        await _context.SaveChangesAsync(cancellationToken);

        // Enqueued after the save so the worker cannot pick up an id that is not committed yet.
        _backgroundJobClient.Enqueue<IDocumentProcessingJob>(job => job.ProcessDocumentAsync(document.Id, CancellationToken.None));

        return Result<DocumentDto>.Success(document.ToDto(), 201);
    }
}
