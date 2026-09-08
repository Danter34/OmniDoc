using Microsoft.AspNetCore.Diagnostics;
using OmniDoc.Application.Common.Exceptions;

namespace OmniDoc.API.Services;

public sealed class ForbiddenExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ForbiddenException || context.Response.HasStarted) return false;
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { errors = new[] { exception.Message } }, cancellationToken);
        return true;
    }
}
