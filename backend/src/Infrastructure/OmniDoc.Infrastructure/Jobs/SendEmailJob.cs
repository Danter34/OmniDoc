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
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SendEmailJob> _logger;

    public SendEmailJob(
        IApplicationDbContext context,
        IEmailSender emailSender,
        IEmailTemplateBuilder templates,
        IEmailVerificationOtpService otpService,
        TimeProvider timeProvider,
        ILogger<SendEmailJob> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _templates = templates;
        _otpService = otpService;
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
            user.EmailConfirmed ||
            user.EmailVerificationOtpHash != message.OtpHash ||
            message.ProtectedPayload is null)
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
            if (message.Type != EmailOutboxType.EmailVerificationOtp)
            {
                throw new InvalidOperationException(
                    $"Unsupported email outbox type '{message.Type}'.");
            }

            var otp = _otpService.Unprotect(message.ProtectedPayload);
            var content = _templates.BuildEmailVerificationOtp(
                user.FullName,
                otp,
                user.OtpExpiresAt ?? now);

            await _emailSender.SendEmailAsync(
                message.RecipientEmail,
                content.Subject,
                content.HtmlBody,
                cancellationToken);

            message.ProcessedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            message.ProtectedPayload = null;
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
}
