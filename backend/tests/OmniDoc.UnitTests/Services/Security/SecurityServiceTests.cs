using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OmniDoc.Domain.Entities;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Services.Security;

namespace OmniDoc.UnitTests.Services.Security;

public sealed class SecurityServiceTests
{
    [Fact]
    public void PasswordHasher_RoundTripsPasswordAndRejectsWrongPassword()
    {
        var hasher = new PasswordHasher();
        const string password = "StrongPassword123!";

        var hash = hasher.HashPassword(password);

        Assert.NotEqual(password, hash);
        Assert.True(hasher.VerifyPassword(password, hash));
        Assert.False(hasher.VerifyPassword("WrongPassword", hash));
        Assert.False(hasher.VerifyPassword(password, "ACCOUNT_DISABLED"));
    }

    [Fact]
    public void JwtTokenGenerator_CreatesSignedTokenWithRequiredClaims()
    {
        var settings = new JwtSettings
        {
            Secret = "ThisIsATestOnlyJwtSecretThatIsLongEnoughForHmacSha256!",
            Issuer = "OmniDocApi",
            Audience = "OmniDocClient",
            ExpiryMinutes = 60
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            FullName = "Workspace Owner"
        };
        var generator = new JwtTokenGenerator(Options.Create(settings));

        var token = generator.GenerateToken(user);

        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(settings.Secret)),
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            },
            out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(user.Email, principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal(user.FullName, principal.FindFirstValue(ClaimTypes.Name));
        Assert.NotNull(principal.FindFirst(JwtRegisteredClaimNames.Jti));
    }
}
