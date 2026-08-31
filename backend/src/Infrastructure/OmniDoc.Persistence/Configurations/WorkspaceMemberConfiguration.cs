using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Persistence.Configurations;

public sealed class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.HasKey(member => new { member.WorkspaceId, member.UserId });

        builder.HasOne(member => member.Workspace)
            .WithMany(workspace => workspace.Members)
            .HasForeignKey(member => member.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(member => member.User)
            .WithMany(user => user.WorkspaceMemberships)
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
