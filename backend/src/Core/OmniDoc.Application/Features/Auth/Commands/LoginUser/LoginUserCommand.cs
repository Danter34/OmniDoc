using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Auth.DTOs;

namespace OmniDoc.Application.Features.Auth.Commands.LoginUser;

public record LoginUserCommand(
    string Email,
    string Password) : IRequest<Result<AuthResponseDto>>;

public sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .EmailAddress().WithMessage("Địa chỉ email không hợp lệ.")
            .MaximumLength(320).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Email");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .MaximumLength(128).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Mật khẩu");
    }
}

public sealed class LoginUserCommandHandler
    : IRequestHandler<LoginUserCommand, Result<AuthResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IShowcasePolicy? _showcasePolicy;

    public LoginUserCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IShowcasePolicy? showcasePolicy = null)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _showcasePolicy = showcasePolicy;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        LoginUserCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        if (user is null ||
            !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Result<AuthResponseDto>.Failure("Email hoặc mật khẩu không chính xác.", 401);
        }

        if (_showcasePolicy is not null)
        {
            try
            {
                _showcasePolicy.EnsureCanSignIn(user.Id, user.Email);
            }
            catch (Common.Exceptions.ForbiddenException ex)
            {
                return Result<AuthResponseDto>.Failure(ex.Message, 403);
            }
        }

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(
                user.Id,
                user.Email,
                user.FullName,
                _tokenGenerator.GenerateToken(user),
                user.EmailConfirmed,
                user.EmailConfirmed
                    ? null
                    : user.LastOtpSentAt?.Add(EmailVerificationPolicy.ResendCooldown)));
    }
}
