using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Invitations.DTOs;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Invitations.Commands.AcceptWorkspaceInvitation;

public sealed record AcceptWorkspaceInvitationCommand(string Token)
    : IRequest<Result<AcceptedInvitationDto>>;

public sealed class AcceptWorkspaceInvitationCommandHandler
    : IRequestHandler<AcceptWorkspaceInvitationCommand, Result<AcceptedInvitationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AcceptWorkspaceInvitationCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<AcceptedInvitationDto>> Handle(
        AcceptWorkspaceInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Authentication is required.",
                401);
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Invitation token is required.",
                400);
        }

        var invitation = await _context.WorkspaceInvitations
            .Include(item => item.Workspace)
            .FirstOrDefaultAsync(
                item => item.Token == request.Token,
                cancellationToken);

        if (invitation is null)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Invitation was not found.",
                404);
        }

        if (invitation.Status == InvitationStatus.Pending &&
            invitation.ExpiresAt <= DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await _context.SaveChangesAsync(cancellationToken);

            return Result<AcceptedInvitationDto>.Failure(
                "Invitation has expired.",
                410);
        }

        if (invitation.Status == InvitationStatus.Expired)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Invitation has expired.",
                410);
        }

        if (invitation.Status == InvitationStatus.Revoked)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Invitation has been revoked.",
                410);
        }

        if (invitation.Status == InvitationStatus.Accepted)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Invitation has already been accepted.",
                409);
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Authenticated user was not found.",
                401);
        }

        if (!string.Equals(
                user.Email.Trim(),
                invitation.InviteeEmail.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<AcceptedInvitationDto>.Failure(
                "This invitation was issued to a different email address.",
                403);
        }

        var isAlreadyMember = await _context.WorkspaceMembers
            .AnyAsync(
                member => member.WorkspaceId == invitation.WorkspaceId &&
                          member.UserId == userId,
                cancellationToken);

        if (isAlreadyMember)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "You are already a member of this workspace.",
                409);
        }

        _context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = invitation.WorkspaceId,
            UserId = userId,
            Role = invitation.Role,
            JoinedAtUtc = DateTime.UtcNow
        });

        invitation.Status = InvitationStatus.Accepted;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<AcceptedInvitationDto>.Success(
            new AcceptedInvitationDto(
                invitation.WorkspaceId,
                invitation.Workspace!.Name,
                invitation.Role.ToString()));
    }
}
