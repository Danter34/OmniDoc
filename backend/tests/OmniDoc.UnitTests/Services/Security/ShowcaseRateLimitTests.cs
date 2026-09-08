using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmniDoc.API.Services;
using OmniDoc.Application.Common.Exceptions;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Services.Security;
using OmniDoc.UnitTests.Features.Auth;

namespace OmniDoc.UnitTests.Services.Security;

public sealed class ShowcaseRateLimitTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void PerIpBudget_IsSharedAcrossEndpoints_AndIgnoresSpoofedHeaders(int limit)
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(c => ApiRateLimits.PerIp(c, limit));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
        for (var i = 0; i < limit; i++)
        {
            context.Request.Headers["X-Forwarded-For"] = $"198.51.100.{i}";
            using var permit = limiter.AttemptAcquire(context);
            Assert.True(permit.IsAcquired);
        }
        using var rejected = limiter.AttemptAcquire(context);
        Assert.False(rejected.IsAcquired);
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.2");
        using var anotherIp = limiter.AttemptAcquire(context);
        Assert.True(anotherIp.IsAcquired);
    }

    [Fact]
    public void ShowcaseConcurrency_IsGlobalAcrossIps_AndReleasedOnDispose()
    {
        var id = Guid.NewGuid();
        var workspace = Guid.NewGuid();
        using var services = new ServiceCollection()
            .AddSingleton<IShowcasePolicy>(new ShowcasePolicy(Options.Create(new ShowcaseSettings { Enabled = true, UserId = id, WorkspaceId = workspace })))
            .AddSingleton<ICurrentUserService>(new StubCurrentUserService { UserId = id, IsAuthenticated = true })
            .BuildServiceProvider();
        using var limiter = ApiRateLimits.CreateShowcaseConcurrencyLimiter();
        HttpContext Request(int index)
        {
            var context = new DefaultHttpContext { RequestServices = services };
            context.Connection.RemoteIpAddress = IPAddress.Parse($"192.0.2.{index}");
            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new EnableRateLimitingAttribute(ApiRateLimits.ChatPolicy)), "chat"));
            return context;
        }
        var leases = Enumerable.Range(1, 4).Select(i => limiter.AttemptAcquire(Request(i))).ToArray();
        try
        {
            Assert.All(leases, lease => Assert.True(lease.IsAcquired));
            using var rejected = limiter.AttemptAcquire(Request(5));
            Assert.False(rejected.IsAcquired);
            leases[0].Dispose();
            using var released = limiter.AttemptAcquire(Request(6));
            Assert.True(released.IsAcquired);
            using var ordinaryEndpoint = limiter.AttemptAcquire(new DefaultHttpContext());
            Assert.True(ordinaryEndpoint.IsAcquired);
        }
        finally { foreach (var lease in leases) lease.Dispose(); }
    }

    [Fact]
    public async Task ForbiddenException_Produces403Json()
    {
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;
        var handled = await new ForbiddenExceptionHandler().TryHandleAsync(context, new ForbiddenException(ShowcasePolicy.DisabledMessage), default);
        Assert.True(handled);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Contains("errors", System.Text.Encoding.UTF8.GetString(body.ToArray()));
    }
}
