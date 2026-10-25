using Microsoft.Extensions.Options;

namespace TaskTracker.Api.Infrastructure.Storage;

/// <summary>
/// Stores files on the local disk. The client never controls the stored name: it is a GUID, so path traversal
/// through a file name is impossible; every resolved path is additionally checked to stay inside the root.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IOptions<FileStorageOptions> options, IHostEnvironment environment, ILogger<LocalFileStorage> logger)
    {
        _logger = logger;
        var root = options.Value.RootPath;
        _root = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(environment.ContentRootPath, root));
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string folder, string extension, Stream content, CancellationToken ct = default)
    {
        var relative = $"{folder}/{Guid.NewGuid():N}{extension}";
        var fullPath = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        try
        {
            await using var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(target, ct);
        }
        catch
        {
            TryDelete(fullPath);
            throw;
        }

        return relative;
    }

    public Stream? OpenRead(string storagePath)
    {
        var fullPath = Resolve(storagePath);
        return File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
    }

    public void Delete(string storagePath) => TryDelete(Resolve(storagePath));

    private string Resolve(string storagePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, storagePath));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage path escapes the storage root.");
        }

        return fullPath;
    }

    private void TryDelete(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException ex)
        {
            // Not fatal for the request: an orphaned file is harmless, a failed response is not.
            _logger.LogWarning(ex, "Could not delete file {Path}", fullPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Could not delete file {Path}", fullPath);
        }
    }
}
