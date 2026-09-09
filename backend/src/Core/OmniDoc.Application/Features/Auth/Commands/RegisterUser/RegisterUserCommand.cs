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
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .EmailAddress().WithMessage("Địa chỉ email không hợp lệ.")
            .MaximumLength(320).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Email");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")
            .MaximumLength(128).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Mật khẩu");

        RuleFor(command => command.FullName)
            .NotEmpty().WithMessage("{PropertyName} không được để trống.")
            .MaximumLength(200).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Họ và tên");
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
                "Địa chỉ email này đã được đăng ký trong hệ thống.",
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
