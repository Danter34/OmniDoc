namespace OmniDoc.Application.Common.Interfaces;

public sealed record NotificationRealtimeMessage(
    Guid Id,
    string Title,
    string Message,
    string? ActionUrl,
    string Type,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string? MetadataJson);

public interface INotificationRealtimePublisher
{
    Task PublishAsync(
        Guid userId,
        NotificationRealtimeMessage notification,
        CancellationToken cancellationToken = default);
}
