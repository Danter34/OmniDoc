using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Persistence.Configurations;

public sealed class DocumentArtifactConfiguration : IEntityTypeConfiguration<DocumentArtifact>
{
    public void Configure(EntityTypeBuilder<DocumentArtifact> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasOne<Document>().WithMany(d => d.Artifacts).HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.DocumentId, a.Kind, a.Generation }).IsUnique();
        builder.Property(a => a.Generation).HasDefaultValue(1);
        builder.Property(a => a.FileName).HasMaxLength(512).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(256).IsRequired();
        builder.Property(a => a.StoragePath).HasMaxLength(1024).IsRequired();
        builder.Property(a => a.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Producer).HasMaxLength(128).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("CK_DocumentArtifacts_Generation", "\"Generation\" >= 1"));
    }
}
