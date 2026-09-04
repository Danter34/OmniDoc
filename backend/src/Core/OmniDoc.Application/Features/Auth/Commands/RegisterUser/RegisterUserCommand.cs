using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Auth.DTOs;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Application.Features.Auth.Commands.RegisterUser;

public record RegisterUserCommand(
    string Email,
    string Password,
    string FullName) : IRequest<Result<AuthResponseDto>>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);

        RuleFor(command => command.FullName)
            .NotEmpty()
            .MaximumLength(200);
    }
}

public sealed class RegisterUserCommandHandler
    : IRequestHandler<RegisterUserCommand, Result<AuthResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IEmailVerificationOtpService _otpService;
    private readonly IEmailOutboxScheduler _emailScheduler;
    private readonly TimeProvider _timeProvider;

    public RegisterUserCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IEmailVerificationOtpService otpService,
        IEmailOutboxScheduler emailScheduler,
        TimeProvider timeProvider)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _otpService = otpService;
        _emailScheduler = emailScheduler;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (await _context.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            return Result<AuthResponseDto>.Failure(
                "An account with this email already exists.",
                409);
        }

        var user = new User
        {
            Email = normalizedEmail,
            FullName = request.FullName.Trim()
        };

        user.PasswordHash = _passwordHasher.HashPassword(request.Password);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var outboxCreation = EmailVerificationOutboxFactory.Create(
            user,
            now,
            _otpService);

        _context.Users.Add(user);
        _context.EmailOutboxMessages.Add(outboxCreation.OutboxMessage);
        await _context.SaveChangesAsync(cancellationToken);
        _emailScheduler.Enqueue(outboxCreation.OutboxMessage.Id);

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(
                user.Id,
                user.Email,
                user.FullName,
                _tokenGenerator.GenerateToken(user),
                user.EmailConfirmed,
                user.LastOtpSentAt?.Add(EmailVerificationPolicy.ResendCooldown)),
            201);
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();
}
