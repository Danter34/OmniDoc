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
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .EmailAddress().WithMessage("Địa chỉ email không hợp lệ.")
            .MaximumLength(320).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Email");
        RuleFor(command => command.Token)
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .MaximumLength(512).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Mã xác thực");
        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .MaximumLength(128).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Mật khẩu mới");
    }
}

public sealed class ResetPasswordCommandHandler
    : IRequestHandler<ResetPasswordCommand, Result<PasswordResetResultDto>>
{
    private const string InvalidTokenMessage =
        "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";

    private readonly IShowcasePolicy _showcase;
    private readonly IApplicationDbContext _context;
    private readonly IPasswordResetTokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public ResetPasswordCommandHandler(
        IShowcasePolicy showcase,
        IApplicationDbContext context,
        IPasswordResetTokenService tokenService,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider)
    {
        _showcase = showcase;
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

        if (user is not null) _showcase.EnsureCanModifyAccount(user.Id);

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

        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            return Result<PasswordResetResultDto>.Failure(
                "Mật khẩu mới không được trùng với mật khẩu cũ gần nhất.", 400);
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
