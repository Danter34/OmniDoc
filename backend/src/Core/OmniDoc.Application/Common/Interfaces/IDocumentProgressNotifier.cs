namespace OmniDoc.Application.Common.Interfaces;

public static class DocumentProcessingStage
{
    public const string Extracting = "Extracting";
    public const string Chunking = "Chunking";
    public const string Embedding = "Embedding";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public record DocumentProgressNotification(
    Guid DocumentId,
    Guid WorkspaceId,
    int ProgressPercentage,
    string Stage,
    string? ErrorMessage = null);

public interface IDocumentProgressNotifier
{
    Task NotifyProgressAsync(DocumentProgressNotification notification, CancellationToken cancellationToken = default);
}
