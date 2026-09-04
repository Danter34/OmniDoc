using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace OmniDoc.API.Services;

public sealed class JwtUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var value = connection.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    connection.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(value, out var userId)
            ? userId.ToString()
            : null;
    }
}
