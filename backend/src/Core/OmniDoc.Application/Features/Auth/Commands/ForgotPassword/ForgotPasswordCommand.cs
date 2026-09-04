using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Auth.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Auth.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email)
    : IRequest<Result<ForgotPasswordDto>>;

public sealed class ForgotPasswordCommandValidator
    : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
    }
}

public sealed class ForgotPasswordCommandHandler
    : IRequestHandler<ForgotPasswordCommand, Result<ForgotPasswordDto>>
{
    public const string NeutralMessage =
        "Nếu email tồn tại, hướng dẫn đặt lại mật khẩu đã được gửi.";

    private readonly IApplicationDbContext _context;
    private readonly IPasswordResetTokenService _tokenService;
    private readonly IPasswordResetLinkService _resetLinks;
    private readonly IEmailVerificationFeatureOptions _featureOptions;
    private readonly IEmailOutboxScheduler _emailScheduler;
    private readonly TimeProvider _timeProvider;

    public ForgotPasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordResetTokenService tokenService,
        IPasswordResetLinkService resetLinks,
        IEmailVerificationFeatureOptions featureOptions,
        IEmailOutboxScheduler emailScheduler,
        TimeProvider timeProvider)
    {
        _context = context;
        _tokenService = tokenService;
        _resetLinks = resetLinks;
        _featureOptions = featureOptions;
        _emailScheduler = emailScheduler;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ForgotPasswordDto>> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(
                item => item.Email == normalizedEmail,
                cancellationToken);

        if (user is null)
        {
            return Result<ForgotPasswordDto>.Success(
                new ForgotPasswordDto(NeutralMessage, null));
        }

        var stalePayloads = await _context.EmailOutboxMessages
            .Where(item =>
                item.UserId == user.Id &&
                item.Type == EmailOutboxType.PasswordReset &&
                item.ProtectedPayload != null)
            .ToListAsync(cancellationToken);

        foreach (var stalePayload in stalePayloads)
        {
            stalePayload.ProtectedPayload = null;
        }

        var creation = PasswordResetOutboxFactory.Create(
            user,
            _timeProvider.GetUtcNow().UtcDateTime,
            _tokenService);

        _context.EmailOutboxMessages.Add(creation.OutboxMessage);
        await _context.SaveChangesAsync(cancellationToken);
        _emailScheduler.Enqueue(creation.OutboxMessage.Id);

        var debugResetUrl = _featureOptions.ShowDemoOtp
            ? _resetLinks.BuildRelativeUrl(creation.RawToken, user.Email)
            : null;

        return Result<ForgotPasswordDto>.Success(
            new ForgotPasswordDto(NeutralMessage, debugResetUrl));
    }
}
