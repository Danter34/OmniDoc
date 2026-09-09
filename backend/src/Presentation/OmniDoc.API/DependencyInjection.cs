using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
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
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = _ =>
                    new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new
                    {
                        errors = new[] { "Dữ liệu yêu cầu không hợp lệ. Vui lòng kiểm tra các trường đã nhập." }
                    }))
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter()));
        services.AddOpenApi();
        services.AddExceptionHandler<ForbiddenExceptionHandler>();
        services.AddExceptionHandler<SystemExceptionHandler>();
        services.AddProblemDetails();
        services.AddApiRateLimits();
        services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
            // Default loopback trust remains. Add only the immediate, trusted edge proxy.
            foreach (var proxy in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                if (!string.IsNullOrWhiteSpace(proxy)) options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
        });
        services.AddSignalR();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IInvitationLinkService, InvitationLinkService>();
        services.AddSingleton<IUserIdProvider, JwtUserIdProvider>();

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
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        var message = context.AuthenticateFailure is SecurityTokenExpiredException
                            ? "Phiên xác thực đã hết hạn. Vui lòng thử lại."
                            : "Bạn không có quyền thực hiện thao tác này.";
                        await context.Response.WriteAsJsonAsync(new { errors = new[] { message } });
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            errors = new[] { "Bạn không có quyền thực hiện thao tác này." }
                        });
                    },
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        var requestPath = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (requestPath.StartsWithSegments("/hubs/document-progress") ||
                             requestPath.StartsWithSegments("/hubs/notifications")))
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
                            context.Fail("Mã xác thực không hợp lệ hoặc đã hết hạn.");
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
                            context.Fail("Phiên đăng nhập đã bị thu hồi. Vui lòng đăng nhập lại.");
                        }
                    }
                };
            });

        services.AddAuthorization();

        // IHubContext is a singleton, so the notifier can be resolved from the Hangfire
        // job scope without capturing a shorter-lived dependency.
        services.AddSingleton<IDocumentProgressNotifier, SignalRDocumentProgressNotifier>();
        services.AddSingleton<INotificationRealtimePublisher, SignalRNotificationRealtimePublisher>();

        return services;
    }
}
