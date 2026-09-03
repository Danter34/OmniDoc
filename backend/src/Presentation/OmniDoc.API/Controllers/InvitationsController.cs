using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Invitations.Commands.AcceptWorkspaceInvitation;
using OmniDoc.Application.Features.Invitations.DTOs;
using OmniDoc.Application.Features.Invitations.Queries.GetInvitationDetails;

namespace OmniDoc.API.Controllers;

public sealed class InvitationsController : BaseApiController
{
    [AllowAnonymous]
    [HttpGet("{token}")]
    public async Task<ActionResult<InvitationDetailsDto>> GetDetails(
        string token,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new GetInvitationDetailsQuery(token),
            cancellationToken));
    }

    [Authorize]
    [HttpPost("{token}/accept")]
    public async Task<ActionResult<AcceptedInvitationDto>> Accept(
        string token,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new AcceptWorkspaceInvitationCommand(token),
            cancellationToken));
    }
}
