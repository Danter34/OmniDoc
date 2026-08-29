namespace OmniDoc.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, Guid workspaceId, CancellationToken cancellationToken = default);

    Task<Stream?> GetFileAsync(string storagePath, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default);
}
