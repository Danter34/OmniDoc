using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using Pgvector;

namespace OmniDoc.Persistence.Contexts;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public const int EmbeddingDimensions = 768;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Citation> Citations => Set<Citation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.Entity<Workspace>(builder =>
        {
            builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(1024);
            builder.HasIndex(x => x.OwnerId);
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Documents).WithOne(x => x.Workspace).HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.Conversations).WithOne(x => x.Workspace).HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Document>(builder =>
        {
            builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
            builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
            builder.Property(x => x.ContentType).HasMaxLength(256).IsRequired();
            builder.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
            builder.HasIndex(x => new { x.WorkspaceId, x.Status });
            builder.HasMany(x => x.Chunks).WithOne(x => x.Document).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentChunk>(builder =>
        {
            builder.Property(x => x.Content).IsRequired();
            builder.HasIndex(x => new { x.DocumentId, x.ChunkIndex }).IsUnique();
            builder.Property(x => x.Embedding)
                .HasColumnType($"vector({EmbeddingDimensions})")
                .HasConversion(EmbeddingConverter, EmbeddingComparer);
        });

        modelBuilder.Entity<Conversation>(builder =>
        {
            builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
            builder.HasMany(x => x.Messages).WithOne(x => x.Conversation).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(builder =>
        {
            builder.Property(x => x.Content).IsRequired();
            builder.HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });
            builder.HasMany(x => x.Citations).WithOne(x => x.ChatMessage).HasForeignKey(x => x.ChatMessageId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Citation>(builder =>
        {
            builder.Property(x => x.DocumentTitle).HasMaxLength(512).IsRequired();
            builder.Property(x => x.Excerpt).IsRequired();
            builder.HasIndex(x => x.ChatMessageId);
        });
    }

    private static readonly ValueConverter<float[]?, Vector?> EmbeddingConverter =
        new(value => value == null ? null : new Vector(value),
            value => value == null ? null : value.Memory.ToArray());

    private static readonly ValueComparer<float[]?> EmbeddingComparer =
        new((left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
            value => value == null ? 0 : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            value => value == null ? null : value.ToArray());
}
