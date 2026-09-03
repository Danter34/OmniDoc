using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Workspaces.Commands.CreateWorkspace;
using OmniDoc.Application.Features.Workspaces.Commands.InviteWorkspaceMember;
using OmniDoc.Application.Features.Workspaces.Commands.RemoveWorkspaceMember;
using OmniDoc.Application.Features.Workspaces.Commands.UpdateMemberRole;
using OmniDoc.Application.Features.Workspaces.DTOs;
using OmniDoc.Application.Features.Workspaces.Queries.GetWorkspaceMembers;
using OmniDoc.Application.Features.Workspaces.Queries.GetWorkspaces;
using OmniDoc.Domain.Enums;

namespace OmniDoc.API.Controllers;

[Authorize]
public class WorkspacesController : BaseApiController
{
    [HttpPost]
    public async Task<ActionResult<WorkspaceDto>> Create(CreateWorkspaceCommand command, CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(command, cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkspaceDto>>> GetAll(CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(new GetWorkspacesQuery(), cancellationToken));
    }

    [HttpGet("{workspaceId:guid}/members")]
    public async Task<ActionResult<List<WorkspaceMemberDto>>> GetMembers(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new GetWorkspaceMembersQuery(workspaceId),
            cancellationToken));
    }

    [HttpPost("{workspaceId:guid}/invitations")]
    public async Task<ActionResult<WorkspaceInvitationDto>> InviteMember(
        Guid workspaceId,
        InviteWorkspaceMemberRequest request,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new InviteWorkspaceMemberCommand(workspaceId, request.Email, request.Role),
            cancellationToken));
    }

    [HttpPatch("{workspaceId:guid}/members/{memberUserId:guid}/role")]
    public async Task<ActionResult<WorkspaceMemberDto>> UpdateMemberRole(
        Guid workspaceId,
        Guid memberUserId,
        UpdateMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new UpdateMemberRoleCommand(workspaceId, memberUserId, request.Role),
            cancellationToken));
    }

    [HttpDelete("{workspaceId:guid}/members/{memberUserId:guid}")]
    public async Task<ActionResult<bool>> RemoveMember(
        Guid workspaceId,
        Guid memberUserId,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new RemoveWorkspaceMemberCommand(workspaceId, memberUserId),
            cancellationToken));
    }
}

public sealed record InviteWorkspaceMemberRequest(string Email, WorkspaceRole Role);

public sealed record UpdateMemberRoleRequest(WorkspaceRole Role);
