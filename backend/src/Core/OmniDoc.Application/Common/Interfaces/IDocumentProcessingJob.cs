namespace OmniDoc.Application.Common.Interfaces;

public interface IDocumentProcessingJob
{
    // Upload schedules this interface method, so Hangfire must discover its filter here.
    [Hangfire.DisableConcurrentExecution(timeoutInSeconds: 600)]
    Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
