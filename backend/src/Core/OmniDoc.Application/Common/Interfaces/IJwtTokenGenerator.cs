using OmniDoc.Domain.Entities;

namespace OmniDoc.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
