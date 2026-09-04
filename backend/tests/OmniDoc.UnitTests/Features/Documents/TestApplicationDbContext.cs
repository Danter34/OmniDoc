using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;

namespace OmniDoc.UnitTests.Features.Documents;

/// An in-memory stand-in for the Postgres context. It deliberately skips the pgvector
/// column mapping, which the InMemory provider cannot honour.
internal sealed class TestApplicationDbContext : DbContext, IApplicationDbContext
{
    public TestApplicationDbContext()
        : base(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase($"omnidoc-{Guid.NewGuid():N}")
            .Options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Citation> Citations => Set<Citation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<WorkspaceMember>()
            .HasKey(member => new { member.WorkspaceId, member.UserId });

        modelBuilder.Entity<WorkspaceMember>()
            .HasOne(member => member.Workspace)
            .WithMany(workspace => workspace.Members)
            .HasForeignKey(member => member.WorkspaceId);

        modelBuilder.Entity<WorkspaceMember>()
            .HasOne(member => member.User)
            .WithMany(user => user.WorkspaceMemberships)
            .HasForeignKey(member => member.UserId);

        modelBuilder.Entity<WorkspaceInvitation>()
            .HasIndex(invitation => invitation.Token)
            .IsUnique();

        modelBuilder.Entity<WorkspaceInvitation>()
            .HasOne(invitation => invitation.Workspace)
            .WithMany(workspace => workspace.Invitations)
            .HasForeignKey(invitation => invitation.WorkspaceId);

        modelBuilder.Entity<WorkspaceInvitation>()
            .HasOne(invitation => invitation.Inviter)
            .WithMany(user => user.SentWorkspaceInvitations)
            .HasForeignKey(invitation => invitation.InviterId);

        modelBuilder.Entity<EmailOutboxMessage>()
            .HasIndex(message => message.IdempotencyKey)
            .IsUnique();

        modelBuilder.Entity<EmailOutboxMessage>()
            .HasOne(message => message.User)
            .WithMany(user => user.EmailOutboxMessages)
            .HasForeignKey(message => message.UserId);
    }

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(cancellationToken);
}
