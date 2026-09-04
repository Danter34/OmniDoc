namespace OmniDoc.Application.Common.Interfaces;

public interface IEmailOutboxDispatcher
{
    Task DispatchPendingAsync(CancellationToken cancellationToken = default);
}
