namespace OmniDoc.Application.Features.Auth.DTOs;

public sealed record ForgotPasswordDto(
    string Message,
    string? DebugResetUrl);

public sealed record PasswordResetResultDto(string Message);
