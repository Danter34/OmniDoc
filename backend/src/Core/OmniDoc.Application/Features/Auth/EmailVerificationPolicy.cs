namespace OmniDoc.Application.Features.Auth;

public static class EmailVerificationPolicy
{
    public const int OtpLength = 6;
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
}
