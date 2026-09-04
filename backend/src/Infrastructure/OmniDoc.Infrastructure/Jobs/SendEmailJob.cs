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
            !IsMessageCurrent(message, user, now))
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
            var content = BuildContent(message, user, now);

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

    private static bool IsMessageCurrent(
        Domain.Entities.EmailOutboxMessage message,
        Domain.Entities.User user,
        DateTime now) =>
        message.Type switch
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

    private EmailContent BuildContent(
        Domain.Entities.EmailOutboxMessage message,
        Domain.Entities.User user,
        DateTime now) =>
        message.Type switch
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
}
