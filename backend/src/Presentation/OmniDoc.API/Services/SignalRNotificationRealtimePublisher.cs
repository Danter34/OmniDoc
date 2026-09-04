using Microsoft.AspNetCore.SignalR;
using OmniDoc.API.Hubs;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.API.Services;

public sealed class SignalRNotificationRealtimePublisher
    : INotificationRealtimePublisher
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationRealtimePublisher> _logger;

    public SignalRNotificationRealtimePublisher(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationRealtimePublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PublishAsync(
        Guid userId,
        NotificationRealtimeMessage notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .User(userId.ToString())
                .SendAsync(
                    NotificationHub.ReceiveNotificationEventName,
                    notification,
                    cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Persistence is authoritative. A reconnecting client will recover this
            // notification through the REST endpoint even if a transient push fails.
            _logger.LogWarning(
                exception,
                "Realtime notification {NotificationId} could not be delivered to user {UserId}.",
                notification.Id,
                userId);
        }
    }
}
