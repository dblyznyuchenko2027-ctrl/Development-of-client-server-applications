namespace TaskTracker.Api.Infrastructure.Storage;

/// <summary>Abstraction over the place where attachment bytes live (local disk now, object storage later).</summary>
public interface IFileStorage
{
    /// <summary>Stores the stream under a generated unique name and returns the relative storage path.</summary>
    /// <param name="folder">Logical sub-folder (e.g. the task id).</param>
    /// <param name="extension">Lower-case extension including the dot, e.g. <c>.png</c>.</param>
    Task<string> SaveAsync(string folder, string extension, Stream content, CancellationToken ct = default);

    /// <summary>Opens a stored file for reading; returns <c>null</c> when it is missing.</summary>
    Stream? OpenRead(string storagePath);

    /// <summary>Deletes a stored file; a missing file is not an error.</summary>
    void Delete(string storagePath);
}
