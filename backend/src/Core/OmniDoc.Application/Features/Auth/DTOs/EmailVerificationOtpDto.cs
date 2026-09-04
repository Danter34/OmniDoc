namespace OmniDoc.Application.Features.Auth.DTOs;

public sealed record EmailVerificationOtpDto(
    DateTime ExpiresAt,
    DateTime ResendAvailableAt);
