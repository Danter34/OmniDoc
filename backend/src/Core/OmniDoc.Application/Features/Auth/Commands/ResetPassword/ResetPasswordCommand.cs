using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Auth.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword) : IRequest<Result<PasswordResetResultDto>>;

public sealed class ResetPasswordCommandValidator
    : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
        RuleFor(command => command.Token)
            .NotEmpty()
            .MaximumLength(512);
        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);
    }
}

public sealed class ResetPasswordCommandHandler
    : IRequestHandler<ResetPasswordCommand, Result<PasswordResetResultDto>>
{
    private const string InvalidTokenMessage =
        "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";

    private readonly IApplicationDbContext _context;
    private readonly IPasswordResetTokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public ResetPasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordResetTokenService tokenService,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider)
    {
        _context = context;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PasswordResetResultDto>> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(
                item => item.Email == normalizedEmail,
                cancellationToken);

        if (user is null ||
            user.PasswordResetTokenHash is null ||
            user.PasswordResetExpiresAt is null)
        {
            return InvalidToken();
        }

        if (user.PasswordResetExpiresAt <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            user.InvalidatePasswordResetToken();
            await ClearResetPayloadsAsync(user.Id, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return InvalidToken();
        }

        if (!_tokenService.Verify(
                user.Id,
                request.Token,
                user.PasswordResetTokenHash))
        {
            return InvalidToken();
        }

        user.ResetPassword(_passwordHasher.HashPassword(request.NewPassword));
        await ClearResetPayloadsAsync(user.Id, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<PasswordResetResultDto>.Success(
            new PasswordResetResultDto("Mật khẩu đã được đặt lại thành công."));
    }

    private async Task ClearResetPayloadsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var messages = await _context.EmailOutboxMessages
            .Where(item =>
                item.UserId == userId &&
                item.Type == EmailOutboxType.PasswordReset &&
                item.ProtectedPayload != null)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.ProtectedPayload = null;
        }
    }

    private static Result<PasswordResetResultDto> InvalidToken() =>
        Result<PasswordResetResultDto>.Failure(InvalidTokenMessage, 400);
}
