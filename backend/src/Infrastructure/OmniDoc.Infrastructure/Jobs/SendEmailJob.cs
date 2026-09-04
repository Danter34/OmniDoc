using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Infrastructure.Jobs;

public sealed class SendEmailJob : IEmailOutboxJob
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateBuilder _templates;
    private readonly IEmailVerificationOtpService _otpService;
    private readonly IPasswordResetTokenService _passwordResetTokens;
    private readonly IPasswordResetLinkService _passwordResetLinks;
    private readonly IInvitationLinkService _invitationLinks;
    private readonly IEmailVerificationFeatureOptions _featureOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SendEmailJob> _logger;

    public SendEmailJob(
        IApplicationDbContext context,
        IEmailSender emailSender,
        IEmailTemplateBuilder templates,
        IEmailVerificationOtpService otpService,
        IPasswordResetTokenService passwordResetTokens,
        IPasswordResetLinkService passwordResetLinks,
        IInvitationLinkService invitationLinks,
        IEmailVerificationFeatureOptions featureOptions,
        TimeProvider timeProvider,
        ILogger<SendEmailJob> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _templates = templates;
        _otpService = otpService;
        _passwordResetTokens = passwordResetTokens;
        _passwordResetLinks = passwordResetLinks;
        _invitationLinks = invitationLinks;
        _featureOptions = featureOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ProcessAsync(
        Guid outboxMessageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.EmailOutboxMessages
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == outboxMessageId, cancellationToken);

        if (message is null || message.ProcessedAtUtc is not null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var user = message.User;

        if (user is null ||
            message.ProtectedPayload is null ||
            !await IsMessageCurrentAsync(message, user, now, cancellationToken))
        {
            message.ProcessedAtUtc = now;
            message.ProtectedPayload = null;
            message.LastError = null;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        message.AttemptCount++;
        message.LastAttemptAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var content = await BuildContentAsync(
                message,
                user,
                now,
                cancellationToken);

            await _emailSender.SendEmailAsync(
                message.RecipientEmail,
                content.Subject,
                content.HtmlBody,
                cancellationToken);

            message.ProcessedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            if (message.Type != EmailOutboxType.EmailVerificationOtp ||
                !_featureOptions.ShowDemoOtp)
            {
                message.ProtectedPayload = null;
            }
            message.LastError = null;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            message.LastError = exception.Message.Length <= 2000
                ? exception.Message
                : exception.Message[..2000];
            await _context.SaveChangesAsync(CancellationToken.None);

            _logger.LogWarning(
                exception,
                "Email outbox message {OutboxMessageId} failed on attempt {AttemptCount}.",
                message.Id,
                message.AttemptCount);
            throw;
        }
    }

    private async Task<bool> IsMessageCurrentAsync(
        Domain.Entities.EmailOutboxMessage message,
        Domain.Entities.User user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (message.Type == EmailOutboxType.WorkspaceInvitation)
        {
            if (!Guid.TryParseExact(message.ProtectedPayload, "N", out var invitationId))
            {
                return false;
            }

            return await _context.WorkspaceInvitations
                .AsNoTracking()
                .AnyAsync(
                    invitation =>
                        invitation.Id == invitationId &&
                        invitation.Status == InvitationStatus.Pending &&
                        invitation.ExpiresAt > now,
                    cancellationToken);
        }

        return message.Type switch
        {
            EmailOutboxType.EmailVerificationOtp =>
                !user.EmailConfirmed &&
                user.EmailVerificationOtpHash == message.OtpHash &&
                user.OtpExpiresAt > now,
            EmailOutboxType.PasswordReset =>
                user.PasswordResetTokenHash == message.OtpHash &&
                user.PasswordResetExpiresAt > now,
            _ => true
        };
    }

    private async Task<EmailContent> BuildContentAsync(
        Domain.Entities.EmailOutboxMessage message,
        Domain.Entities.User user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (message.Type == EmailOutboxType.WorkspaceInvitation)
        {
            return await BuildWorkspaceInvitationContentAsync(
                message,
                cancellationToken);
        }

        return message.Type switch
        {
            EmailOutboxType.EmailVerificationOtp =>
                _templates.BuildEmailVerificationOtp(
                    user.FullName,
                    _otpService.Unprotect(message.ProtectedPayload!),
                    user.OtpExpiresAt ?? now),
            EmailOutboxType.PasswordReset =>
                BuildPasswordResetContent(message, user, now),
            _ => throw new InvalidOperationException(
                $"Unsupported email outbox type '{message.Type}'.")
        };
    }

    private EmailContent BuildPasswordResetContent(
        Domain.Entities.EmailOutboxMessage message,
        Domain.Entities.User user,
        DateTime now)
    {
        var rawToken = _passwordResetTokens.Unprotect(message.ProtectedPayload!);
        var resetUrl = _passwordResetLinks.BuildAbsoluteUrl(rawToken, user.Email);

        return _templates.BuildPasswordReset(
            user.FullName,
            resetUrl,
            user.PasswordResetExpiresAt ?? now);
    }

    private async Task<EmailContent> BuildWorkspaceInvitationContentAsync(
        Domain.Entities.EmailOutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(message.ProtectedPayload, "N", out var invitationId))
        {
            throw new InvalidOperationException("Workspace invitation payload is invalid.");
        }

        var invitation = await _context.WorkspaceInvitations
            .AsNoTracking()
            .Where(item => item.Id == invitationId)
            .Select(item => new
            {
                item.InviteeEmail,
                item.Token,
                item.Role,
                item.ExpiresAt,
                WorkspaceName = item.Workspace!.Name,
                InviterName = item.Inviter!.FullName
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Workspace invitation was not found.");

        return _templates.BuildWorkspaceInvitation(
            invitation.InviteeEmail,
            invitation.WorkspaceName,
            invitation.InviterName,
            invitation.Role.ToString(),
            _invitationLinks.BuildInvitationLink(invitation.Token),
            invitation.ExpiresAt);
    }
}
