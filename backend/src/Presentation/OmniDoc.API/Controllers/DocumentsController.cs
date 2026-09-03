using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Documents.Commands.UploadDocument;
using OmniDoc.Application.Features.Documents.DTOs;
using OmniDoc.Application.Features.Documents.Queries.GetDocumentContent;
using OmniDoc.Application.Features.Documents.Queries.GetDocumentById;
using OmniDoc.Application.Features.Documents.Queries.GetDocumentsByWorkspace;

namespace OmniDoc.API.Controllers;

[Authorize]
public class DocumentsController : BaseApiController
{
    [HttpPost("/api/workspaces/{workspaceId:guid}/documents")]
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

        // Ingestion runs as a Hangfire job; the document comes back Pending and progress
        // arrives over the DocumentProgressHub.
        return HandleResult(await Sender.Send(uploadCommand, cancellationToken));
    }

    [HttpGet("/api/workspaces/{workspaceId:guid}/documents")]
    public async Task<ActionResult<List<DocumentDto>>> GetByWorkspace(Guid workspaceId, CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(new GetDocumentsByWorkspaceQuery(workspaceId), cancellationToken));
    }

    [HttpGet("/api/workspaces/{workspaceId:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new GetDocumentContentQuery(workspaceId, documentId),
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            return StatusCode(result.StatusCode, new { errors = result.Errors });
        }

        Response.Headers.ContentDisposition =
            $"inline; filename=\"{Uri.EscapeDataString(result.Data.FileName)}\"";
        Response.Headers.CacheControl = "private, no-store";

        return File(
            result.Data.Stream,
            result.Data.ContentType,
            enableRangeProcessing: true);
    }

    [HttpGet("/api/documents/{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(new GetDocumentByIdQuery(id), cancellationToken));
    }
}
