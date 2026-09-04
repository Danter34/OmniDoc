using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OmniDoc.API.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    public const string ReceiveNotificationEventName = "ReceiveNotification";
}
