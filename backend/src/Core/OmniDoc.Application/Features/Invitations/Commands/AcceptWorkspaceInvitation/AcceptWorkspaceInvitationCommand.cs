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
    private readonly IShowcasePolicy _showcase;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AcceptWorkspaceInvitationCommandHandler(
        IShowcasePolicy showcase,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _showcase = showcase;
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
                "Bạn không có quyền thực hiện thao tác này.",
                401);
        }

        _showcase.EnsureCanModifyAccount(userId);

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Vui lòng cung cấp mã lời mời.",
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
                "Không tìm thấy lời mời.",
                404);
        }

        _showcase.EnsureCanModifyWorkspace(invitation.WorkspaceId);

        if (invitation.Status == InvitationStatus.Pending &&
            invitation.ExpiresAt <= DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await _context.SaveChangesAsync(cancellationToken);

            return Result<AcceptedInvitationDto>.Failure(
                "Lời mời đã hết hạn.",
                410);
        }

        if (invitation.Status == InvitationStatus.Expired)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Lời mời đã hết hạn.",
                410);
        }

        if (invitation.Status == InvitationStatus.Revoked)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Lời mời đã bị thu hồi.",
                410);
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Không tìm thấy tài khoản người dùng.",
                401);
        }

        if (!string.Equals(
                user.Email.Trim(),
                invitation.InviteeEmail.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Lời mời này được gửi đến một địa chỉ email khác.",
                403);
        }

        var existingMember = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(
                member => member.WorkspaceId == invitation.WorkspaceId &&
                          member.UserId == userId,
                cancellationToken);

        if (existingMember is not null)
        {
            if (invitation.Status != InvitationStatus.Accepted)
            {
                invitation.Status = InvitationStatus.Accepted;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result<AcceptedInvitationDto>.Success(new AcceptedInvitationDto(
                invitation.WorkspaceId,
                invitation.Workspace!.Name,
                existingMember.Role.ToString()));
        }

        if (invitation.Status == InvitationStatus.Accepted)
        {
            return Result<AcceptedInvitationDto>.Failure(
                "Lời mời đã được chấp nhận. Vui lòng yêu cầu lời mời mới để tham gia lại.",
                410);
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
