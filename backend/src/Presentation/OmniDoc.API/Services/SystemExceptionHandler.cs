using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace OmniDoc.API.Services;

public sealed class SystemExceptionHandler(ILogger<SystemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted) return false;

        var errors = exception switch
        {
            ValidationException validation => validation.Errors.Select(error => error.ErrorMessage).Distinct().ToArray(),
            BadHttpRequestException => ["Dữ liệu yêu cầu không hợp lệ. Vui lòng kiểm tra và thử lại."],
            UnauthorizedAccessException => ["Bạn không có quyền thực hiện thao tác này."],
            _ => ["Đã có lỗi xảy ra. Vui lòng thử lại sau."]
        };
        context.Response.StatusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            BadHttpRequestException request => request.StatusCode,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };
        logger.LogError(exception, "Request failed with status {StatusCode}.", context.Response.StatusCode);
        await context.Response.WriteAsJsonAsync(new { errors }, cancellationToken);
        return true;
    }
}
