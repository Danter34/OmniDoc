using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.Infrastructure.Jobs;

public sealed class EmailOutboxDispatcher : IEmailOutboxDispatcher
{
    private const int BatchSize = 100;

    private readonly IApplicationDbContext _context;
    private readonly IEmailOutboxScheduler _scheduler;

    public EmailOutboxDispatcher(
        IApplicationDbContext context,
        IEmailOutboxScheduler scheduler)
    {
        _context = context;
        _scheduler = scheduler;
    }

    public async Task DispatchPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var pendingIds = await _context.EmailOutboxMessages
            .AsNoTracking()
            .Where(message => message.ProcessedAtUtc == null)
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => message.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var messageId in pendingIds)
        {
            _scheduler.Enqueue(messageId);
        }
    }
}
