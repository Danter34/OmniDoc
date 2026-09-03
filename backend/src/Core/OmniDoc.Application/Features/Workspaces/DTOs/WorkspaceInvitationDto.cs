namespace OmniDoc.Application.Features.Workspaces.DTOs;

public sealed record WorkspaceInvitationDto(
    Guid Id,
    Guid WorkspaceId,
    string InviteeEmail,
    string Role,
    DateTime ExpiresAt,
    string Status,
    string InviteLink);
