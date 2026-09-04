using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Auth;

public sealed record PasswordResetOutboxCreation(
    EmailOutboxMessage OutboxMessage,
    string RawToken,
    DateTime ExpiresAtUtc);

public static class PasswordResetOutboxFactory
{
    public static PasswordResetOutboxCreation Create(
        User user,
        DateTime issuedAtUtc,
        IPasswordResetTokenService tokenService)
    {
        var issue = tokenService.Create(user.Id, issuedAtUtc);
        user.IssuePasswordResetToken(
            issue.TokenHash,
            issue.ExpiresAtUtc,
            issuedAtUtc);

        var message = new EmailOutboxMessage
        {
            UserId = user.Id,
            RecipientEmail = user.Email,
            Type = EmailOutboxType.PasswordReset,
            ProtectedPayload = issue.ProtectedToken,
            OtpHash = issue.TokenHash,
            CreatedAtUtc = issuedAtUtc
        };

        message.IdempotencyKey = $"password-reset:{user.Id:N}:{message.Id:N}";

        return new PasswordResetOutboxCreation(
            message,
            issue.RawToken,
            issue.ExpiresAtUtc);
    }
}
