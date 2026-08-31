namespace OmniDoc.Application.Features.Auth.DTOs;

public record AuthResponseDto(
    Guid Id,
    string Email,
    string FullName,
    string Token);
