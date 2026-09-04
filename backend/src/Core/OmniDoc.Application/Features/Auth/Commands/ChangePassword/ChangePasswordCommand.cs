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
            .NotEmpty()
            .MaximumLength(128);
        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);
    }
}

public sealed class ChangePasswordCommandHandler
    : IRequestHandler<ChangePasswordCommand, Result<AuthResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public ChangePasswordCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
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
                "Authentication is required.",
                401);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<AuthResponseDto>.Failure(
                "The authenticated user was not found.",
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
