namespace OmniDoc.Application.Features.Invitations.DTOs;

public sealed record AcceptedInvitationDto(
    Guid WorkspaceId,
    string WorkspaceName,
    string Role);
