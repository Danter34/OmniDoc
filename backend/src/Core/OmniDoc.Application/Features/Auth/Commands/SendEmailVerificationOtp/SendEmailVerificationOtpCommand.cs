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
    private readonly IShowcasePolicy _showcase;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailVerificationOtpService _otpService;
    private readonly IEmailVerificationFeatureOptions _featureOptions;
    private readonly IEmailOutboxScheduler _emailScheduler;
    private readonly TimeProvider _timeProvider;

    public SendEmailVerificationOtpCommandHandler(
        IShowcasePolicy showcase,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IEmailVerificationOtpService otpService,
        IEmailVerificationFeatureOptions featureOptions,
        IEmailOutboxScheduler emailScheduler,
        TimeProvider timeProvider)
    {
        _showcase = showcase;
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
                "Bạn không có quyền thực hiện thao tác này.",
                401);
        }

        _showcase.EnsureCanModifyAccount(userId);

        var user = await _context.Users
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<EmailVerificationOtpDto>.Failure(
                "Không tìm thấy tài khoản người dùng.",
                404);
        }

        if (user.EmailConfirmed)
        {
            return Result<EmailVerificationOtpDto>.Failure(
                "Email đã được xác thực.",
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
                "Vui lòng đợi 60 giây trước khi yêu cầu mã xác thực mới.",
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
