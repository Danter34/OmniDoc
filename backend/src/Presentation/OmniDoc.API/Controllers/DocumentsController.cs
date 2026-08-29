using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Documents.Commands.ProcessDocument;
using OmniDoc.Application.Features.Documents.Commands.UploadDocument;
using OmniDoc.Application.Features.Documents.DTOs;
using OmniDoc.Application.Features.Documents.Queries.GetDocumentById;
using OmniDoc.Application.Features.Documents.Queries.GetDocumentsByWorkspace;

namespace OmniDoc.API.Controllers;

public class DocumentsController : BaseApiController
{
    [HttpPost("/api/workspaces/{workspaceId:guid}/documents/upload")]
    public async Task<ActionResult<DocumentDto>> Upload(
        Guid workspaceId,
        IFormFile file,
        [FromForm] string? title,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        await using var stream = file.OpenReadStream();

        var uploadCommand = new UploadDocumentCommand(
            workspaceId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            title);

        var uploadResult = await Sender.Send(uploadCommand, cancellationToken);

        if (!uploadResult.IsSuccess || uploadResult.Data is null)
        {
            return HandleResult(uploadResult);
        }

        var processResult = await Sender.Send(new ProcessDocumentCommand(uploadResult.Data.Id), cancellationToken);

        return HandleResult(processResult);
    }

    [HttpGet("/api/workspaces/{workspaceId:guid}/documents")]
    public async Task<ActionResult<List<DocumentDto>>> GetByWorkspace(Guid workspaceId, CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(new GetDocumentsByWorkspaceQuery(workspaceId), cancellationToken));
    }

    [HttpGet("/api/documents/{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(new GetDocumentByIdQuery(id), cancellationToken));
    }
}
