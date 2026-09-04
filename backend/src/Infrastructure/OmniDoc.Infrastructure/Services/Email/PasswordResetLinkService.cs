using Microsoft.Extensions.Configuration;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.Infrastructure.Services.Email;

public sealed class PasswordResetLinkService : IPasswordResetLinkService
{
    private readonly string _frontendBaseUrl;

    public PasswordResetLinkService(IConfiguration configuration)
    {
        _frontendBaseUrl = (configuration["Frontend:BaseUrl"] ??
                            "http://localhost:3000").TrimEnd('/');
    }

    public string BuildRelativeUrl(string rawToken, string email) =>
        $"/reset-password?token={Uri.EscapeDataString(rawToken)}&email={Uri.EscapeDataString(email)}";

    public string BuildAbsoluteUrl(string rawToken, string email) =>
        $"{_frontendBaseUrl}{BuildRelativeUrl(rawToken, email)}";
}
