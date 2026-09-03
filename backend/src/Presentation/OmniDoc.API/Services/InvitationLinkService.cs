using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.API.Services;

public sealed class InvitationLinkService : IInvitationLinkService
{
    private readonly string _frontendBaseUrl;

    public InvitationLinkService(IConfiguration configuration)
    {
        _frontendBaseUrl = (configuration["Frontend:BaseUrl"] ?? "http://localhost:3000")
            .TrimEnd('/');
    }

    public string BuildInvitationLink(string token) =>
        $"{_frontendBaseUrl}/invitations/{Uri.EscapeDataString(token)}";
}
