namespace OmniDoc.Application.Features.Auth.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    DateTime CreatedAtUtc);
