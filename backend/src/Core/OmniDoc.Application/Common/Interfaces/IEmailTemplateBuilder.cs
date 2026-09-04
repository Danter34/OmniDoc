namespace OmniDoc.Application.Common.Interfaces;

public sealed record EmailContent(string Subject, string HtmlBody);

public interface IEmailTemplateBuilder
{
    EmailContent BuildEmailVerificationOtp(
        string recipientName,
        string otp,
        DateTime expiresAtUtc);

    EmailContent BuildPasswordReset(
        string recipientName,
        string resetUrl,
        DateTime expiresAtUtc);

    EmailContent BuildWorkspaceInvitation(
        string recipientName,
        string workspaceName,
        string inviterName,
        string role,
        string invitationUrl,
        DateTime expiresAtUtc);
}
