namespace OmniDoc.Application.Features.Invitations.DTOs;

public sealed record InvitationDetailsDto(
    Guid WorkspaceId,
    string WorkspaceName,
    string InviterName,
    string Role,
    DateTime ExpiresAt,
    string Status);
