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
    private readonly IEmailOutboxScheduler _emailScheduler;
    private readonly TimeProvider _timeProvider;

    public SendEmailVerificationOtpCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IEmailVerificationOtpService otpService,
        IEmailOutboxScheduler emailScheduler,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _otpService = otpService;
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
            return Result<EmailVerificationOtpDto>.Failure(
                "Please wait 60 seconds before requesting another verification code.",
                429);
        }

        var outboxMessage = EmailVerificationOutboxFactory.Create(
            user,
            now,
            _otpService);

        _context.EmailOutboxMessages.Add(outboxMessage);
        await _context.SaveChangesAsync(cancellationToken);
        _emailScheduler.Enqueue(outboxMessage.Id);

        return Result<EmailVerificationOtpDto>.Success(
            new EmailVerificationOtpDto(
                user.OtpExpiresAt!.Value,
                user.LastOtpSentAt!.Value.Add(EmailVerificationPolicy.ResendCooldown)));
    }
}
