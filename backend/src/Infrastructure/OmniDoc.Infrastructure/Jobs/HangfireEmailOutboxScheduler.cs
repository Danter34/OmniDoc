using Hangfire;
using Microsoft.Extensions.Logging;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.Infrastructure.Jobs;

public sealed class HangfireEmailOutboxScheduler : IEmailOutboxScheduler
{
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<HangfireEmailOutboxScheduler> _logger;

    public HangfireEmailOutboxScheduler(
        IBackgroundJobClient backgroundJobs,
        ILogger<HangfireEmailOutboxScheduler> logger)
    {
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    public void Enqueue(Guid outboxMessageId)
    {
        try
        {
            _backgroundJobs.Enqueue<IEmailOutboxJob>(job =>
                job.ProcessAsync(outboxMessageId, CancellationToken.None));
        }
        catch (Exception exception)
        {
            // The durable outbox dispatcher will recover this message on its next run.
            _logger.LogWarning(
                exception,
                "Could not immediately enqueue email outbox message {OutboxMessageId}.",
                outboxMessageId);
        }
    }
}
