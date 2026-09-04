using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Application.Features.Notifications.DTOs;

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    string? ActionUrl,
    string Type,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string? MetadataJson)
{
    public static NotificationDto FromEntity(Notification notification) => new(
        notification.Id,
        notification.Title,
        notification.Message,
        notification.ActionUrl,
        notification.Type.ToString(),
        notification.IsRead,
        notification.CreatedAt,
        notification.ReadAt,
        notification.MetadataJson);

    public NotificationRealtimeMessage ToRealtimeMessage() => new(
        Id,
        Title,
        Message,
        ActionUrl,
        Type,
        IsRead,
        CreatedAt,
        ReadAt,
        MetadataJson);
}

public sealed record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record NotificationCountDto(int Count);
