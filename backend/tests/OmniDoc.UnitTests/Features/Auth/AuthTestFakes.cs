using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;

namespace OmniDoc.UnitTests.Features.Auth;

internal sealed class StubCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; init; }

    public string? Email { get; init; }

    public bool IsAuthenticated { get; init; }
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"hashed::{password}";

    public bool VerifyPassword(string password, string passwordHash) =>
        passwordHash == HashPassword(password);
}

internal sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    public string GenerateToken(User user) => $"token::{user.Id}";
}

internal sealed class FakeEmailVerificationOtpService
    : IEmailVerificationOtpService
{
    public const string Otp = "123456";

    public EmailVerificationOtpIssue Create(Guid userId, DateTime issuedAtUtc) =>
        new(
            Otp,
            Hash(userId, Otp),
            $"protected::{Otp}",
            issuedAtUtc.AddMinutes(10));

    public bool Verify(Guid userId, string otp, string expectedHash) =>
        expectedHash == Hash(userId, otp);

    public string Unprotect(string protectedOtp) =>
        protectedOtp.Replace("protected::", string.Empty, StringComparison.Ordinal);

    public static string Hash(Guid userId, string otp) =>
        $"hash::{userId:N}::{otp}";
}

internal sealed class FakePasswordResetTokenService
    : IPasswordResetTokenService
{
    public const string Token = "test-reset-token";

    public PasswordResetTokenIssue Create(Guid userId, DateTime issuedAtUtc) =>
        new(
            Token,
            Hash(userId, Token),
            $"protected-reset::{Token}",
            issuedAtUtc.AddMinutes(15));

    public bool Verify(Guid userId, string rawToken, string expectedHash) =>
        expectedHash == Hash(userId, rawToken);

    public string Unprotect(string protectedToken) =>
        protectedToken.Replace(
            "protected-reset::",
            string.Empty,
            StringComparison.Ordinal);

    public static string Hash(Guid userId, string rawToken) =>
        $"reset-hash::{userId:N}::{rawToken}";
}

internal sealed class FakePasswordResetLinkService
    : IPasswordResetLinkService
{
    public string BuildRelativeUrl(string rawToken, string email) =>
        $"/reset-password?token={rawToken}&email={Uri.EscapeDataString(email)}";

    public string BuildAbsoluteUrl(string rawToken, string email) =>
        $"https://app.example.test{BuildRelativeUrl(rawToken, email)}";
}

internal sealed class FakeInvitationLinkService : IInvitationLinkService
{
    public string BuildInvitationLink(string token) =>
        $"https://app.example.test/invitations/{token}";
}

internal sealed class FakeEmailOutboxScheduler : IEmailOutboxScheduler
{
    public List<Guid> EnqueuedMessageIds { get; } = [];

    public void Enqueue(Guid outboxMessageId) =>
        EnqueuedMessageIds.Add(outboxMessageId);
}

internal sealed class StubEmailVerificationFeatureOptions(
    bool showDemoOtp = false) : IEmailVerificationFeatureOptions
{
    public bool ShowDemoOtp { get; } = showDemoOtp;
}

internal sealed class StubTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } =
        new(2026, 9, 4, 5, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
