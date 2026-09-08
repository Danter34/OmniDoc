using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.API.Services;

public static class ApiRateLimits
{
    public const string LoginPolicy = "LoginPolicy";
    public const string ChatPolicy = "ChatPolicy";

    // The server connection address, after trusted-proxy processing; never a raw header.
    public static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";

    public static RateLimitPartition<string> PerIp(HttpContext context, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });

    public static PartitionedRateLimiter<HttpContext> CreateShowcaseConcurrencyLimiter() =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName != ChatPolicy)
                return RateLimitPartition.GetNoLimiter("other-endpoints");

            var policy = context.RequestServices.GetRequiredService<IShowcasePolicy>();
            var currentUser = context.RequestServices.GetRequiredService<ICurrentUserService>();
            var sampleUser = currentUser.UserId is { } id && policy.IsShowcaseUser(id);
            var sampleWorkspace = Guid.TryParse(context.Request.RouteValues["workspaceId"]?.ToString(), out var workspaceId)
                && policy.IsShowcaseWorkspace(workspaceId);

            // One shared partition for every sample user/workspace request, including SSE.
            return sampleUser || sampleWorkspace
                ? RateLimitPartition.GetConcurrencyLimiter("showcase", _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 4,
                    QueueLimit = 0
                })
                : RateLimitPartition.GetNoLimiter("normal-users");
        });

    public static IServiceCollection AddApiRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginPolicy, context => PerIp(context, 5));
            options.AddPolicy(ChatPolicy, context => PerIp(context, 6));
            options.GlobalLimiter = CreateShowcaseConcurrencyLimiter();
            options.OnRejected = async (context, token) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    errors = new[] { "Bạn đang thao tác quá nhanh, vui lòng thử lại sau giây lát." }
                }, token);
            };
        });
        return services;
    }
}
