using OmniDoc.Domain.Entities;

namespace OmniDoc.Application.Common.Interfaces;

public static class AuthClaimTypes
{
    public const string TokenVersion = "token_version";
}

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
