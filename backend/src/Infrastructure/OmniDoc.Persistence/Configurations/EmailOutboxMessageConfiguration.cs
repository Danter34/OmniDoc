using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Persistence.Configurations;

public sealed class EmailOutboxMessageConfiguration
    : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.HasKey(message => message.Id);

        builder.Property(message => message.RecipientEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(message => message.ProtectedPayload)
            .HasMaxLength(2048);

        builder.Property(message => message.OtpHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(message => message.IdempotencyKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(message => message.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(message => message.IdempotencyKey)
            .IsUnique();

        builder.HasIndex(message => new
        {
            message.ProcessedAtUtc,
            message.CreatedAtUtc
        });

        builder.HasOne(message => message.User)
            .WithMany(user => user.EmailOutboxMessages)
            .HasForeignKey(message => message.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
