using Hangfire;

namespace OmniDoc.Application.Common.Interfaces;

public interface IEmailOutboxJob
{
    [AutomaticRetry(
        Attempts = 5,
        DelaysInSeconds = [10, 30, 60, 180, 300],
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    Task ProcessAsync(
        Guid outboxMessageId,
        CancellationToken cancellationToken = default);
}
