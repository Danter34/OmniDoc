using Microsoft.AspNetCore.SignalR;
using OmniDoc.API.Hubs;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.API.Services;

public class SignalRDocumentProgressNotifier : IDocumentProgressNotifier
{
    private readonly IHubContext<DocumentProgressHub> _hubContext;

    public SignalRDocumentProgressNotifier(IHubContext<DocumentProgressHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyProgressAsync(DocumentProgressNotification notification, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(DocumentProgressHub.GroupName(notification.WorkspaceId))
            .SendAsync(DocumentProgressHub.ProgressEventName, notification, cancellationToken);
}
