using FluentValidation;
using Hangfire;
using MediatR;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Documents.DTOs;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

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
            .Must(name => Path.GetExtension(name).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only PDF files are supported.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB.");
    }
}

public class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, Result<DocumentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public UploadDocumentCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorage,
        IBackgroundJobClient backgroundJobClient,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _fileStorage = fileStorage;
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

        var storagePath = await _fileStorage.SaveFileAsync(request.FileStream, request.FileName, request.WorkspaceId, cancellationToken);

        var document = new Document
        {
            WorkspaceId = request.WorkspaceId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? Path.GetFileNameWithoutExtension(request.FileName) : request.Title,
            FileName = Path.GetFileName(request.FileName),
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            StoragePath = storagePath,
            Status = DocumentStatus.Pending
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync(cancellationToken);

        // Enqueued after the save so the worker cannot pick up an id that is not committed yet.
        _backgroundJobClient.Enqueue<IDocumentProcessingJob>(job => job.ProcessDocumentAsync(document.Id, CancellationToken.None));

        return Result<DocumentDto>.Success(document.ToDto(), 201);
    }
}
