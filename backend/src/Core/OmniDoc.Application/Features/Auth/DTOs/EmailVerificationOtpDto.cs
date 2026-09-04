namespace OmniDoc.Application.Features.Auth.DTOs;

public sealed record EmailVerificationOtpDto(
    bool Success,
    int ResendCooldownSeconds,
    string? DebugOtp,
    DateTime ExpiresAt,
    DateTime ResendAvailableAt);
