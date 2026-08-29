using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private const string WorkspacesFolder = "workspaces";

    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        var configuredRoot = configuration["FileStorage:RootPath"];
        var resolvedRoot = string.IsNullOrWhiteSpace(configuredRoot) ? "uploads" : configuredRoot;

        // Relative roots resolve against the content root, not the binary output
        // directory, so stored files survive a rebuild that clears bin/.
        _rootPath = Path.GetFullPath(resolvedRoot, Directory.GetCurrentDirectory());
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            throw new ArgumentException("File name is invalid.", nameof(fileName));
        }

        var relativeDirectory = Path.Combine(WorkspacesFolder, workspaceId.ToString());
        var absoluteDirectory = Path.Combine(_rootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(safeName)}";
        var absolutePath = Path.Combine(absoluteDirectory, storedName);

        await using (var destination = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(destination, cancellationToken);
        }

        _logger.LogInformation("Stored file for workspace {WorkspaceId} as {StoredName}", workspaceId, storedName);

        return Path.Combine(relativeDirectory, storedName).Replace(Path.DirectorySeparatorChar, '/');
    }

    public Task<Stream?> GetFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWithinRoot(storagePath);

        if (!File.Exists(absolutePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWithinRoot(storagePath);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    private string ResolveWithinRoot(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("Storage path is required.", nameof(storagePath));
        }

        var absolutePath = Path.GetFullPath(Path.Combine(_rootPath, storagePath));
        var rootPrefix = _rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!absolutePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Storage path '{storagePath}' resolves outside the storage root.");
        }

        return absolutePath;
    }
}
