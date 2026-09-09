using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Workspaces.DTOs;
using OmniDoc.Application.Features.Notifications.DTOs;
using OmniDoc.Application.Features.Workspaces;
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
        RuleFor(command => command.WorkspaceId).NotEmpty().WithMessage("{PropertyName} không được để trống.").WithName("Mã không gian làm việc");
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .EmailAddress().WithMessage("Địa chỉ email không hợp lệ.")
            .MaximumLength(320).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Email");
        RuleFor(command => command.Role).IsInEnum().WithMessage("{PropertyName} không hợp lệ.").WithName("Vai trò");
    }
}

public sealed class InviteWorkspaceMemberCommandHandler
    : IRequestHandler<InviteWorkspaceMemberCommand, Result<WorkspaceInvitationDto>>
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private readonly IShowcasePolicy _showcase;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;
    private readonly IEmailOutboxScheduler _emailScheduler;
    private readonly INotificationRealtimePublisher _notificationPublisher;
    private readonly TimeProvider _timeProvider;

    public InviteWorkspaceMemberCommandHandler(
        IShowcasePolicy showcase,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IWorkspaceAuthorizationService workspaceAuthorization,
        IEmailOutboxScheduler emailScheduler,
        INotificationRealtimePublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        _showcase = showcase;
        _context = context;
        _currentUser = currentUser;
        _workspaceAuthorization = workspaceAuthorization;
        _emailScheduler = emailScheduler;
        _notificationPublisher = notificationPublisher;
        _timeProvider = timeProvider;
    }

    public async Task<Result<WorkspaceInvitationDto>> Handle(
        InviteWorkspaceMemberCommand request,
        CancellationToken cancellationToken)
    {
        _showcase.EnsureCanModifyWorkspace(request.WorkspaceId);

        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
            WorkspacePermission.InviteMembers,
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
                "Bạn không có quyền thực hiện thao tác này.",
                401);
        }

        var inviter = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == inviterId)
            .Select(user => new { user.EmailConfirmed, user.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        if (inviter is null)
        {
            return Result<WorkspaceInvitationDto>.Failure(
                "Không tìm thấy tài khoản người dùng.",
                404);
        }

        if (!inviter.EmailConfirmed)
        {
            return Result<WorkspaceInvitationDto>.Failure(
                "Bạn cần xác minh email để sử dụng tính năng mời thành viên bảo mật này.",
                403,
                "EMAIL_NOT_VERIFIED");
        }

        if (!Enum.IsDefined(request.Role))
        {
            return Result<WorkspaceInvitationDto>.Failure(
                "Vai trò trong lời mời không hợp lệ.",
                400);
        }

        if (access.Data!.Role == WorkspaceRole.Admin &&
            request.Role != WorkspaceRole.Member)
        {
            return Result<WorkspaceInvitationDto>.Failure(
                "Quản trị viên chỉ có thể mời người dùng với vai trò thành viên.",
                403);
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
                "Email này đã là thành viên của không gian làm việc.",
                409);
        }

        var workspaceName = await _context.Workspaces
            .AsNoTracking()
            .Where(workspace => workspace.Id == request.WorkspaceId)
            .Select(workspace => workspace.Name)
            .FirstAsync(cancellationToken);

        var invitee = await _context.Users
            .AsNoTracking()
            .Where(user => user.Email == normalizedEmail)
            .Select(user => new { user.Id })
            .FirstOrDefaultAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
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
                "Email này đã có lời mời đang chờ phản hồi.",
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

        var outboxMessage = WorkspaceInvitationOutboxFactory.Create(invitation);
        Notification? notification = null;

        if (invitee is not null)
        {
            var roleName = request.Role switch
            {
                WorkspaceRole.Owner => "chủ sở hữu",
                WorkspaceRole.Admin => "quản trị viên",
                _ => "thành viên"
            };
            notification = new Notification
            {
                UserId = invitee.Id,
                Title = "Lời mời tham gia không gian làm việc",
                Message = $"{inviter.FullName} đã mời bạn tham gia {workspaceName} với vai trò {roleName}.",
                ActionUrl = $"/invitations/accept?token={Uri.EscapeDataString(invitation.Token)}",
                Type = NotificationType.WorkspaceInvitation,
                CreatedAt = now,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    workspaceId = request.WorkspaceId,
                    invitationId = invitation.Id,
                    role = request.Role.ToString()
                })
            };
        }

        _context.WorkspaceInvitations.Add(invitation);
        _context.EmailOutboxMessages.Add(outboxMessage);
        if (notification is not null)
        {
            _context.Notifications.Add(notification);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _emailScheduler.Enqueue(outboxMessage.Id);

        if (notification is not null && invitee is not null)
        {
            await _notificationPublisher.PublishAsync(
                invitee.Id,
                NotificationDto.FromEntity(notification).ToRealtimeMessage(),
                cancellationToken);
        }

        return Result<WorkspaceInvitationDto>.Success(
            new WorkspaceInvitationDto(
                invitation.Id,
                invitation.WorkspaceId,
                invitation.InviteeEmail,
                invitation.Role.ToString(),
                invitation.ExpiresAt,
                invitation.Status.ToString()),
            201);
    }

    private static string CreateSecureToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return token.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
