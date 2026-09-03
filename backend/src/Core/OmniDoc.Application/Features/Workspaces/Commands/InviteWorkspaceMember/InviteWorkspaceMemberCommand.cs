using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Workspaces.DTOs;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Workspaces.Commands.InviteWorkspaceMember;

public sealed record InviteWorkspaceMemberCommand(
    Guid WorkspaceId,
    string Email,
    WorkspaceRole Role) : IRequest<Result<WorkspaceInvitationDto>>;

public sealed class InviteWorkspaceMemberCommandValidator
    : AbstractValidator<InviteWorkspaceMemberCommand>
{
    public InviteWorkspaceMemberCommandValidator()
    {
        RuleFor(command => command.WorkspaceId).NotEmpty();
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
        RuleFor(command => command.Role).IsInEnum();
    }
}

public sealed class InviteWorkspaceMemberCommandHandler
    : IRequestHandler<InviteWorkspaceMemberCommand, Result<WorkspaceInvitationDto>>
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IInvitationLinkService _invitationLinks;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public InviteWorkspaceMemberCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IInvitationLinkService invitationLinks,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _currentUser = currentUser;
        _invitationLinks = invitationLinks;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<WorkspaceInvitationDto>> Handle(
        InviteWorkspaceMemberCommand request,
        CancellationToken cancellationToken)
    {
        var access = await _workspaceAuthorization.AuthorizeOwnerAsync(
            request.WorkspaceId,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<WorkspaceInvitationDto>.Failure(
                access.Errors,
                access.StatusCode);
        }

        if (_currentUser.UserId is not { } inviterId)
        {
            return Result<WorkspaceInvitationDto>.Failure(
                "Authentication is required.",
                401);
        }

        if (!Enum.IsDefined(request.Role))
        {
            return Result<WorkspaceInvitationDto>.Failure(
                "Invitation role is invalid.",
                400);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var isAlreadyMember = await _context.WorkspaceMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.WorkspaceId == request.WorkspaceId &&
                member.User!.Email == normalizedEmail,
                cancellationToken);

        if (isAlreadyMember)
        {
            return Result<WorkspaceInvitationDto>.Failure(
                "This email is already a member of the workspace.",
                409);
        }

        var now = DateTime.UtcNow;
        var existingInvitations = await _context.WorkspaceInvitations
            .Where(invitation =>
                invitation.WorkspaceId == request.WorkspaceId &&
                invitation.InviteeEmail == normalizedEmail &&
                invitation.Status == InvitationStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var expiredInvitation in existingInvitations.Where(
                     invitation => invitation.ExpiresAt <= now))
        {
            expiredInvitation.Status = InvitationStatus.Expired;
        }

        if (existingInvitations.Any(invitation => invitation.ExpiresAt > now))
        {
            return Result<WorkspaceInvitationDto>.Failure(
                "A pending invitation already exists for this email.",
                409);
        }

        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = request.WorkspaceId,
            InviterId = inviterId,
            InviteeEmail = normalizedEmail,
            Token = CreateSecureToken(),
            Role = request.Role,
            ExpiresAt = now.Add(InvitationLifetime),
            Status = InvitationStatus.Pending,
            CreatedAt = now
        };

        _context.WorkspaceInvitations.Add(invitation);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<WorkspaceInvitationDto>.Success(
            new WorkspaceInvitationDto(
                invitation.Id,
                invitation.WorkspaceId,
                invitation.InviteeEmail,
                invitation.Role.ToString(),
                invitation.ExpiresAt,
                invitation.Status.ToString(),
                _invitationLinks.BuildInvitationLink(invitation.Token)),
            201);
    }

    private static string CreateSecureToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return token.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
