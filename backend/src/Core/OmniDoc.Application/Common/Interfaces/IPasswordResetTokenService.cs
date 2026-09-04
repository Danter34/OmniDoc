namespace OmniDoc.Application.Common.Interfaces;

public sealed record PasswordResetTokenIssue(
    string RawToken,
    string TokenHash,
    string ProtectedToken,
    DateTime ExpiresAtUtc);

public interface IPasswordResetTokenService
{
    PasswordResetTokenIssue Create(Guid userId, DateTime issuedAtUtc);

    bool Verify(Guid userId, string rawToken, string expectedHash);

    string Unprotect(string protectedToken);
}

public interface IPasswordResetLinkService
{
    string BuildRelativeUrl(string rawToken, string email);

    string BuildAbsoluteUrl(string rawToken, string email);
}
