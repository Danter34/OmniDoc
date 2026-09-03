using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Invitations.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Invitations.Queries.GetInvitationDetails;

public sealed record GetInvitationDetailsQuery(string Token)
    : IRequest<Result<InvitationDetailsDto>>;

public sealed class GetInvitationDetailsQueryHandler
    : IRequestHandler<GetInvitationDetailsQuery, Result<InvitationDetailsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInvitationDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<InvitationDetailsDto>> Handle(
        GetInvitationDetailsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result<InvitationDetailsDto>.Failure(
                "Invitation token is required.",
                400);
        }

        var invitation = await _context.WorkspaceInvitations
            .AsNoTracking()
            .Where(item => item.Token == request.Token)
            .Select(item => new
            {
                item.WorkspaceId,
                WorkspaceName = item.Workspace!.Name,
                InviterName = item.Inviter!.FullName,
                item.Role,
                item.ExpiresAt,
                item.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (invitation is null)
        {
            return Result<InvitationDetailsDto>.Failure(
                "Invitation was not found.",
                404);
        }

        var effectiveStatus =
            invitation.Status == InvitationStatus.Pending &&
            invitation.ExpiresAt <= DateTime.UtcNow
                ? InvitationStatus.Expired
                : invitation.Status;

        return Result<InvitationDetailsDto>.Success(
            new InvitationDetailsDto(
                invitation.WorkspaceId,
                invitation.WorkspaceName,
                invitation.InviterName,
                invitation.Role.ToString(),
                invitation.ExpiresAt,
                effectiveStatus.ToString()));
    }
}
