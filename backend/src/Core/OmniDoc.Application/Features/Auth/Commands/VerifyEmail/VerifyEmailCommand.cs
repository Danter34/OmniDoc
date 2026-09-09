using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Auth.DTOs;

namespace OmniDoc.Application.Features.Auth.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string Otp) : IRequest<Result<UserDto>>;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(command => command.Otp)
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .Matches("^[0-9]{6}$")
            .WithMessage("Mã xác thực phải gồm đúng 6 chữ số.").WithName("Mã xác thực");
    }
}

public sealed class VerifyEmailCommandHandler
    : IRequestHandler<VerifyEmailCommand, Result<UserDto>>
{
    private readonly IShowcasePolicy _showcase;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailVerificationOtpService _otpService;
    private readonly TimeProvider _timeProvider;

    public VerifyEmailCommandHandler(
        IShowcasePolicy showcase,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IEmailVerificationOtpService otpService,
        TimeProvider timeProvider)
    {
        _showcase = showcase;
        _context = context;
        _currentUser = currentUser;
        _otpService = otpService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<UserDto>> Handle(
        VerifyEmailCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<UserDto>.Failure("Bạn không có quyền thực hiện thao tác này.", 401);
        }

        _showcase.EnsureCanModifyAccount(userId);

        var user = await _context.Users
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<UserDto>.Failure(
                "Không tìm thấy tài khoản người dùng.",
                404);
        }

        if (user.EmailConfirmed)
        {
            return Result<UserDto>.Success(ToDto(user));
        }

        if (user.OtpFailedAttempts >= EmailVerificationPolicy.MaxFailedAttempts)
        {
            user.InvalidateEmailVerificationOtp();
            await ClearProtectedOtpPayloadsAsync(user.Id, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<UserDto>.Failure(
                "Bạn đã nhập sai quá nhiều lần. Vui lòng yêu cầu mã xác thực mới.",
                429);
        }

        if (user.EmailVerificationOtpHash is null || user.OtpExpiresAt is null)
        {
            return Result<UserDto>.Failure(
                "Không có mã xác thực còn hiệu lực. Vui lòng yêu cầu mã mới.",
                400);
        }

        if (user.OtpExpiresAt <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            user.InvalidateEmailVerificationOtp();
            await ClearProtectedOtpPayloadsAsync(user.Id, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<UserDto>.Failure(
                "Phiên xác thực đã hết hạn. Vui lòng thử lại.",
                410);
        }

        if (!_otpService.Verify(user.Id, request.Otp, user.EmailVerificationOtpHash))
        {
            var failedAttempts = user.RecordFailedOtpAttempt();
            if (failedAttempts >= EmailVerificationPolicy.MaxFailedAttempts)
            {
                user.InvalidateEmailVerificationOtp();
                await ClearProtectedOtpPayloadsAsync(user.Id, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result<UserDto>.Failure(
                failedAttempts >= EmailVerificationPolicy.MaxFailedAttempts
                    ? "Bạn đã nhập sai quá nhiều lần. Vui lòng yêu cầu mã xác thực mới."
                    : "Mã xác thực không hợp lệ hoặc đã hết hạn.",
                failedAttempts >= EmailVerificationPolicy.MaxFailedAttempts ? 429 : 400);
        }

        user.ConfirmEmail();
        await ClearProtectedOtpPayloadsAsync(user.Id, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<UserDto>.Success(ToDto(user));
    }

    private static UserDto ToDto(Domain.Entities.User user) =>
        new(
            user.Id,
            user.Email,
            user.FullName,
            user.CreatedAtUtc,
            user.EmailConfirmed,
            user.EmailConfirmed
                ? null
                : user.LastOtpSentAt?.Add(EmailVerificationPolicy.ResendCooldown));

    private async Task ClearProtectedOtpPayloadsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var messages = await _context.EmailOutboxMessages
            .Where(item => item.UserId == userId && item.ProtectedPayload != null)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.ProtectedPayload = null;
        }
    }
}
