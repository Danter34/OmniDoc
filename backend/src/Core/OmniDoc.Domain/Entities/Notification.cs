using OmniDoc.Domain.Enums;

namespace OmniDoc.Domain.Entities;

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? ActionUrl { get; set; }

    public NotificationType Type { get; set; }

    public bool IsRead { get; private set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; private set; }

    public string? MetadataJson { get; set; }

    public User? User { get; set; }

    public bool MarkAsRead(DateTime readAtUtc)
    {
        if (IsRead)
        {
            return false;
        }

        IsRead = true;
        ReadAt = readAtUtc;
        return true;
    }
}
