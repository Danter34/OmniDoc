using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Auth.DTOs;

namespace OmniDoc.Application.Features.Auth.Commands.SendEmailVerificationOtp;

public sealed record SendEmailVerificationOtpCommand
    : IRequest<Result<EmailVerificationOtpDto>>;

public sealed class SendEmailVerificationOtpCommandHandler
    : IRequestHandler<SendEmailVerificationOtpCommand, Result<EmailVerificationOtpDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailVerificationOtpService _otpService;
    private readonly IEmailVerificationFeatureOptions _featureOptions;
    private readonly IEmailOutboxScheduler _emailScheduler;
    private readonly TimeProvider _timeProvider;

    public SendEmailVerificationOtpCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IEmailVerificationOtpService otpService,
        IEmailVerificationFeatureOptions featureOptions,
        IEmailOutboxScheduler emailScheduler,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _otpService = otpService;
        _featureOptions = featureOptions;
        _emailScheduler = emailScheduler;
        _timeProvider = timeProvider;
    }

    public async Task<Result<EmailVerificationOtpDto>> Handle(
        SendEmailVerificationOtpCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<EmailVerificationOtpDto>.Failure(
                "Authentication is required.",
                401);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<EmailVerificationOtpDto>.Failure(
                "The authenticated user was not found.",
                404);
        }

        if (user.EmailConfirmed)
        {
            return Result<EmailVerificationOtpDto>.Failure(
                "Email is already verified.",
                409);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var resendAvailableAt = user.LastOtpSentAt?.Add(
            EmailVerificationPolicy.ResendCooldown);

        if (resendAvailableAt > now)
        {
            var existingOtp = _featureOptions.ShowDemoOtp
                ? await GetActiveDemoOtpAsync(userId, user.EmailVerificationOtpHash, cancellationToken)
                : null;

            if (existingOtp is not null && user.OtpExpiresAt > now)
            {
                return Result<EmailVerificationOtpDto>.Success(
                    CreateResponse(
                        user.OtpExpiresAt.Value,
                        resendAvailableAt.Value,
                        existingOtp,
                        now));
            }

            return Result<EmailVerificationOtpDto>.Failure(
                "Please wait 60 seconds before requesting another verification code.",
                429);
        }

        var stalePayloads = await _context.EmailOutboxMessages
            .Where(item => item.UserId == userId && item.ProtectedPayload != null)
            .ToListAsync(cancellationToken);

        foreach (var stalePayload in stalePayloads)
        {
            stalePayload.ProtectedPayload = null;
        }

        var outboxCreation = EmailVerificationOutboxFactory.Create(
            user,
            now,
            _otpService);

        _context.EmailOutboxMessages.Add(outboxCreation.OutboxMessage);
        await _context.SaveChangesAsync(cancellationToken);
        _emailScheduler.Enqueue(outboxCreation.OutboxMessage.Id);

        return Result<EmailVerificationOtpDto>.Success(
            CreateResponse(
                user.OtpExpiresAt!.Value,
                user.LastOtpSentAt!.Value.Add(EmailVerificationPolicy.ResendCooldown),
                _featureOptions.ShowDemoOtp ? outboxCreation.RawOtp : null,
                now));
    }

    private async Task<string?> GetActiveDemoOtpAsync(
        Guid userId,
        string? otpHash,
        CancellationToken cancellationToken)
    {
        if (otpHash is null)
        {
            return null;
        }

        var protectedOtp = await _context.EmailOutboxMessages
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId &&
                item.OtpHash == otpHash &&
                item.ProtectedPayload != null)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.ProtectedPayload)
            .FirstOrDefaultAsync(cancellationToken);

        return protectedOtp is null ? null : _otpService.Unprotect(protectedOtp);
    }

    private static EmailVerificationOtpDto CreateResponse(
        DateTime expiresAt,
        DateTime resendAvailableAt,
        string? debugOtp,
        DateTime now) =>
        new(
            true,
            Math.Max(0, (int)Math.Ceiling((resendAvailableAt - now).TotalSeconds)),
            debugOtp,
            expiresAt,
            resendAvailableAt);
}
