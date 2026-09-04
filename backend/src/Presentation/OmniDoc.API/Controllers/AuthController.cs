using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Auth.Commands.LoginUser;
using OmniDoc.Application.Features.Auth.Commands.ChangePassword;
using OmniDoc.Application.Features.Auth.Commands.ForgotPassword;
using OmniDoc.Application.Features.Auth.Commands.RegisterUser;
using OmniDoc.Application.Features.Auth.Commands.ResetPassword;
using OmniDoc.Application.Features.Auth.Commands.SendEmailVerificationOtp;
using OmniDoc.Application.Features.Auth.Commands.VerifyEmail;
using OmniDoc.Application.Features.Auth.DTOs;
using OmniDoc.Application.Features.Auth.Queries.GetCurrentUser;

namespace OmniDoc.API.Controllers;

public record RegisterRequest(string Email, string Password, string FullName);

public record LoginRequest(string Email, string Password);

public record VerifyEmailRequest(string Otp);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

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

    [Authorize]
    [HttpPost("send-verification-otp")]
    public async Task<ActionResult<EmailVerificationOtpDto>> SendVerificationOtp(
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new SendEmailVerificationOtpCommand(),
            cancellationToken));
    }

    [Authorize]
    [HttpPost("verify-email")]
    public async Task<ActionResult<UserDto>> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new VerifyEmailCommand(request.Otp),
            cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordDto>> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new ForgotPasswordCommand(request.Email),
            cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<ActionResult<PasswordResetResultDto>> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new ResetPasswordCommand(
                request.Email,
                request.Token,
                request.NewPassword),
            cancellationToken));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<AuthResponseDto>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(
            new ChangePasswordCommand(
                request.CurrentPassword,
                request.NewPassword),
            cancellationToken));
    }
}
