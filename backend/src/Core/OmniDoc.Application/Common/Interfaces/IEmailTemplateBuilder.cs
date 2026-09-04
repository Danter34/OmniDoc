namespace OmniDoc.Application.Common.Interfaces;

public sealed record EmailContent(string Subject, string HtmlBody);

public interface IEmailTemplateBuilder
{
    EmailContent BuildEmailVerificationOtp(
        string recipientName,
        string otp,
        DateTime expiresAtUtc);
}
