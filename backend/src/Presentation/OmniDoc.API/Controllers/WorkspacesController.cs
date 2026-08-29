using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Workspaces.Commands.CreateWorkspace;
using OmniDoc.Application.Features.Workspaces.DTOs;
using OmniDoc.Application.Features.Workspaces.Queries.GetWorkspaces;

namespace OmniDoc.API.Controllers;

public class WorkspacesController : BaseApiController
{
    [HttpPost]
    public async Task<ActionResult<WorkspaceDto>> Create(CreateWorkspaceCommand command, CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(command, cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkspaceDto>>> GetAll([FromQuery] string? userId, CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(new GetWorkspacesQuery(userId), cancellationToken));
    }
}
