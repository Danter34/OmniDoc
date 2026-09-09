using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OmniDoc.API.Services;

namespace OmniDoc.UnitTests.Services.Security;

public sealed class SystemExceptionHandlerTests
{
    [Theory]
    [InlineData(500, "Đã có lỗi xảy ra. Vui lòng thử lại sau.")]
    [InlineData(403, "Bạn không có quyền thực hiện thao tác này.")]
    [InlineData(400, "Dữ liệu yêu cầu không hợp lệ. Vui lòng kiểm tra và thử lại.")]
    public async Task UnexpectedErrors_ReturnVietnameseWithoutInternalDetails(int statusCode, string message)
    {
        Exception exception = statusCode switch
        {
            403 => new UnauthorizedAccessException("Private storage path"),
            400 => new BadHttpRequestException("Internal parser details"),
            _ => new InvalidOperationException("Upstream account and provider details")
        };

        var (actualStatus, errors) = await HandleAsync(exception);
        Assert.Equal(statusCode, actualStatus);
        Assert.Equal(message, Assert.Single(errors));
    }

    [Fact]
    public async Task ValidationErrors_Return400WithDistinctFieldMessages()
    {
        const string message = "Mật khẩu mới không được trùng với mật khẩu hiện tại.";
        var exception = new ValidationException([
            new ValidationFailure("NewPassword", message),
            new ValidationFailure("NewPassword", message)
        ]);
        var (status, errors) = await HandleAsync(exception);
        Assert.Equal(400, status);
        Assert.Equal(message, Assert.Single(errors));
    }

    private static async Task<(int Status, string[] Errors)> HandleAsync(Exception exception)
    {
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;
        var handler = new SystemExceptionHandler(NullLogger<SystemExceptionHandler>.Instance);
        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        return (context.Response.StatusCode, json.RootElement.GetProperty("errors")
            .EnumerateArray().Select(error => error.GetString()!).ToArray());
    }
}
