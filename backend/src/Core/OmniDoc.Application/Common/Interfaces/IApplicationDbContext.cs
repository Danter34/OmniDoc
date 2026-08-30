using Microsoft.EntityFrameworkCore;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Workspace> Workspaces { get; }
    DbSet<WorkspaceMember> WorkspaceMembers { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentChunk> DocumentChunks { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<Citation> Citations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
