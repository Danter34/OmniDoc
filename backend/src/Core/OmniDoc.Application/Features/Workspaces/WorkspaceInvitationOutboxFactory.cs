using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Workspaces;

public static class WorkspaceInvitationOutboxFactory
{
    public static EmailOutboxMessage Create(WorkspaceInvitation invitation)
    {
        var message = new EmailOutboxMessage
        {
            UserId = invitation.InviterId,
            RecipientEmail = invitation.InviteeEmail,
            Type = EmailOutboxType.WorkspaceInvitation,
            ProtectedPayload = invitation.Id.ToString("N"),
            OtpHash = invitation.Id.ToString("N"),
            CreatedAtUtc = invitation.CreatedAt
        };

        message.IdempotencyKey = $"workspace-invitation:{invitation.Id:N}";
        return message;
    }
}
