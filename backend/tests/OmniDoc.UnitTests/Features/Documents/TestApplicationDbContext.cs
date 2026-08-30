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

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Citation> Citations => Set<Citation>();

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(cancellationToken);
}
