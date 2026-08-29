using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

    public UploadDocumentCommandHandler(IApplicationDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<Result<DocumentDto>> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        var workspaceExists = await _context.Workspaces
            .AnyAsync(w => w.Id == request.WorkspaceId, cancellationToken);

        if (!workspaceExists)
        {
            return Result<DocumentDto>.Failure($"Workspace '{request.WorkspaceId}' was not found.", 404);
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

        return Result<DocumentDto>.Success(document.ToDto(), 201);
    }
}
