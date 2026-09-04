using MediatR;
using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Common.Models;

namespace OmniDoc.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    private ISender? _sender;

    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected ActionResult<T> HandleResult<T>(Result<T> result)
    {
        if (!result.IsSuccess)
        {
            return StatusCode(
                result.StatusCode,
                new { errors = result.Errors, errorCode = result.ErrorCode });
        }

        if (result.StatusCode == StatusCodes.Status201Created)
        {
            return StatusCode(StatusCodes.Status201Created, result.Data);
        }

        return Ok(result.Data);
    }
}
