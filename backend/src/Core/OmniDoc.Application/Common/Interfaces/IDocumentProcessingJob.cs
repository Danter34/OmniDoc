namespace OmniDoc.Application.Common.Interfaces;

public interface IDocumentProcessingJob
{
    Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
