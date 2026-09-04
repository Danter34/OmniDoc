namespace OmniDoc.Application.Common.Interfaces;

public sealed record EmailVerificationOtpIssue(
    string OtpHash,
    string ProtectedOtp,
    DateTime ExpiresAtUtc);

public interface IEmailVerificationOtpService
{
    EmailVerificationOtpIssue Create(Guid userId, DateTime issuedAtUtc);

    bool Verify(Guid userId, string otp, string expectedHash);

    string Unprotect(string protectedOtp);
}
