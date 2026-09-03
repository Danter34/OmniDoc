using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Persistence.Configurations;

public sealed class WorkspaceInvitationConfiguration
    : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.InviteeEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(invitation => invitation.Token)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(invitation => invitation.Token)
            .IsUnique();

        builder.HasIndex(invitation => invitation.InviteeEmail);

        builder.HasIndex(invitation => new
        {
            invitation.WorkspaceId,
            invitation.InviteeEmail,
            invitation.Status
        });

        builder.HasOne(invitation => invitation.Workspace)
            .WithMany(workspace => workspace.Invitations)
            .HasForeignKey(invitation => invitation.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(invitation => invitation.Inviter)
            .WithMany(user => user.SentWorkspaceInvitations)
            .HasForeignKey(invitation => invitation.InviterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
