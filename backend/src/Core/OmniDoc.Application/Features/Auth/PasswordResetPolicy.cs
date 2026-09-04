namespace OmniDoc.Application.Features.Auth;

public static class PasswordResetPolicy
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
}
