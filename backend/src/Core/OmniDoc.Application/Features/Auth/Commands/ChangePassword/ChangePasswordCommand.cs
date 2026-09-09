using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Auth.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Auth.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword) : IRequest<Result<AuthResponseDto>>;

public sealed class ChangePasswordCommandValidator
    : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .MaximumLength(128).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Mật khẩu hiện tại");
        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .MaximumLength(128).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.")
            .NotEqual(command => command.CurrentPassword)
            .WithMessage("Mật khẩu mới không được trùng với mật khẩu hiện tại.").WithName("Mật khẩu mới");
    }
}

public sealed class ChangePasswordCommandHandler
    : IRequestHandler<ChangePasswordCommand, Result<AuthResponseDto>>
{
    private readonly IShowcasePolicy _showcase;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public ChangePasswordCommandHandler(
        IShowcasePolicy showcase,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
        _showcase = showcase;
        _context = context;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result<AuthResponseDto>.Failure(
                "Bạn không có quyền thực hiện thao tác này.",
                401);
        }

        _showcase.EnsureCanModifyAccount(userId);

        var user = await _context.Users
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<AuthResponseDto>.Failure(
                "Không tìm thấy tài khoản người dùng.",
                404);
        }

        if (!_passwordHasher.VerifyPassword(
                request.CurrentPassword,
                user.PasswordHash))
        {
            return Result<AuthResponseDto>.Failure(
                "Mật khẩu hiện tại không chính xác.",
                400);
        }

        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            return Result<AuthResponseDto>.Failure(
                "Mật khẩu mới không được trùng với mật khẩu hiện tại.", 400);
        }

        user.ChangePassword(_passwordHasher.HashPassword(request.NewPassword));
        var resetMessages = await _context.EmailOutboxMessages
            .Where(item =>
                item.UserId == user.Id &&
                item.Type == EmailOutboxType.PasswordReset &&
                item.ProtectedPayload != null)
            .ToListAsync(cancellationToken);

        foreach (var resetMessage in resetMessages)
        {
            resetMessage.ProtectedPayload = null;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(
                user.Id,
                user.Email,
                user.FullName,
                _tokenGenerator.GenerateToken(user),
                user.EmailConfirmed,
                user.EmailConfirmed
                    ? null
                    : user.LastOtpSentAt?.Add(
                        EmailVerificationPolicy.ResendCooldown)));
    }
}
