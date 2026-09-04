using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(notification => notification.Message)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(notification => notification.ActionUrl)
            .HasMaxLength(2048);

        builder.Property(notification => notification.MetadataJson)
            .HasColumnType("jsonb");

        builder.HasIndex(notification => new
        {
            notification.UserId,
            notification.IsRead
        });

        builder.HasIndex(notification => new
        {
            notification.UserId,
            notification.CreatedAt
        });

        builder.HasOne(notification => notification.User)
            .WithMany(user => user.Notifications)
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
