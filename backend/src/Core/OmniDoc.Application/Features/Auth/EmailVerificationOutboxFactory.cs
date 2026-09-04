using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Auth;

public static class EmailVerificationOutboxFactory
{
    public static EmailOutboxMessage Create(
        User user,
        DateTime issuedAtUtc,
        IEmailVerificationOtpService otpService)
    {
        var issue = otpService.Create(user.Id, issuedAtUtc);
        user.IssueEmailVerificationOtp(
            issue.OtpHash,
            issuedAtUtc,
            issue.ExpiresAtUtc);

        var message = new EmailOutboxMessage
        {
            UserId = user.Id,
            RecipientEmail = user.Email,
            Type = EmailOutboxType.EmailVerificationOtp,
            ProtectedPayload = issue.ProtectedOtp,
            OtpHash = issue.OtpHash,
            CreatedAtUtc = issuedAtUtc
        };

        message.IdempotencyKey = $"email-verification:{user.Id:N}:{message.Id:N}";
        return message;
    }
}
