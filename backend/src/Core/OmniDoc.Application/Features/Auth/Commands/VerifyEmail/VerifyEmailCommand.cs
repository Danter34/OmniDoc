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
            .NotEmpty()
            .Matches("^[0-9]{6}$")
            .WithMessage("OTP must contain exactly 6 digits.");
    }
}

public sealed class VerifyEmailCommandHandler
    : IRequestHandler<VerifyEmailCommand, Result<UserDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailVerificationOtpService _otpService;
    private readonly TimeProvider _timeProvider;

    public VerifyEmailCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IEmailVerificationOtpService otpService,
        TimeProvider timeProvider)
    {
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
            return Result<UserDto>.Failure("Authentication is required.", 401);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<UserDto>.Failure(
                "The authenticated user was not found.",
                404);
        }

        if (user.EmailConfirmed)
        {
            return Result<UserDto>.Success(ToDto(user));
        }

        if (user.OtpFailedAttempts >= EmailVerificationPolicy.MaxFailedAttempts)
        {
            user.InvalidateEmailVerificationOtp();
            await _context.SaveChangesAsync(cancellationToken);

            return Result<UserDto>.Failure(
                "Too many invalid attempts. Request a new verification code.",
                429);
        }

        if (user.EmailVerificationOtpHash is null || user.OtpExpiresAt is null)
        {
            return Result<UserDto>.Failure(
                "No active verification code. Request a new code.",
                400);
        }

        if (user.OtpExpiresAt <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            user.InvalidateEmailVerificationOtp();
            await _context.SaveChangesAsync(cancellationToken);

            return Result<UserDto>.Failure(
                "Verification code has expired. Request a new code.",
                410);
        }

        if (!_otpService.Verify(user.Id, request.Otp, user.EmailVerificationOtpHash))
        {
            var failedAttempts = user.RecordFailedOtpAttempt();
            if (failedAttempts >= EmailVerificationPolicy.MaxFailedAttempts)
            {
                user.InvalidateEmailVerificationOtp();
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result<UserDto>.Failure(
                failedAttempts >= EmailVerificationPolicy.MaxFailedAttempts
                    ? "Too many invalid attempts. Request a new verification code."
                    : "Verification code is invalid.",
                failedAttempts >= EmailVerificationPolicy.MaxFailedAttempts ? 429 : 400);
        }

        user.ConfirmEmail();
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
}
