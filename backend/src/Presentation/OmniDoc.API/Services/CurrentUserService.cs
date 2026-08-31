using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.API.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email) ??
        Principal?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;
}
