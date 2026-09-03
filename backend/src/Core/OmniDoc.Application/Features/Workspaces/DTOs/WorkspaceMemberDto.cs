namespace OmniDoc.Application.Features.Workspaces.DTOs;

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    DateTime JoinedAt);
