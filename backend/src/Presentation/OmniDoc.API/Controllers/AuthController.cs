using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Auth.Commands.LoginUser;
using OmniDoc.Application.Features.Auth.Commands.RegisterUser;
using OmniDoc.Application.Features.Auth.DTOs;
using OmniDoc.Application.Features.Auth.Queries.GetCurrentUser;

namespace OmniDoc.API.Controllers;

public record RegisterRequest(string Email, string Password, string FullName);

public record LoginRequest(string Email, string Password);

public sealed class AuthController : BaseApiController
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.Email,
            request.Password,
            request.FullName);

        return HandleResult(await Sender.Send(command, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginUserCommand(request.Email, request.Password);

        return HandleResult(await Sender.Send(command, cancellationToken));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(new GetCurrentUserQuery(), cancellationToken));
    }
}
