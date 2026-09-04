using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OmniDoc.API.Services;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.API;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter()));
        services.AddOpenApi();
        services.AddSignalR();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IInvitationLinkService, InvitationLinkService>();

        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>() ?? new JwtSettings();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = System.Security.Claims.ClaimTypes.Name
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments(
                                "/hubs/document-progress"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var userIdValue = context.Principal?
                            .FindFirstValue(ClaimTypes.NameIdentifier) ??
                            context.Principal?
                                .FindFirstValue(JwtRegisteredClaimNames.Sub);
                        var tokenVersionValue = context.Principal?
                            .FindFirstValue(AuthClaimTypes.TokenVersion);

                        if (!Guid.TryParse(userIdValue, out var userId) ||
                            !int.TryParse(tokenVersionValue, out var tokenVersion))
                        {
                            context.Fail("Token session version is invalid.");
                            return;
                        }

                        var validator = context.HttpContext.RequestServices
                            .GetRequiredService<ITokenVersionValidator>();
                        var isCurrent = await validator.IsCurrentAsync(
                            userId,
                            tokenVersion,
                            context.HttpContext.RequestAborted);

                        if (!isCurrent)
                        {
                            context.Fail("This session has been revoked.");
                        }
                    }
                };
            });

        services.AddAuthorization();

        // IHubContext is a singleton, so the notifier can be resolved from the Hangfire
        // job scope without capturing a shorter-lived dependency.
        services.AddSingleton<IDocumentProgressNotifier, SignalRDocumentProgressNotifier>();

        return services;
    }
}
