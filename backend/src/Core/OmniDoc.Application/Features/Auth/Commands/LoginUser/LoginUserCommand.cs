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
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class LoginUserCommandHandler
    : IRequestHandler<LoginUserCommand, Result<AuthResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public LoginUserCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
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
            return Result<AuthResponseDto>.Failure("Invalid email or password.", 401);
        }

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(
                user.Id,
                user.Email,
                user.FullName,
                _tokenGenerator.GenerateToken(user)));
    }
}
