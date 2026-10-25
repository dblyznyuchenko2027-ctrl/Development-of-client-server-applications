using System.ComponentModel.DataAnnotations;

namespace TaskTracker.Api.Infrastructure.Storage;

/// <summary>Settings of the <c>Storage</c> configuration section (uploaded task attachments).</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Directory where files are stored. Relative paths are resolved against the content root.
    /// Must be outside <c>wwwroot</c>: files are only served through the API.
    /// </summary>
    [Required]
    public string RootPath { get; set; } = "uploads";

    /// <summary>Maximum size of one uploaded file in bytes (default 5 MiB).</summary>
    [Range(1, 100 * 1024 * 1024)]
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Maximum number of files that can be attached to one task.</summary>
    [Range(1, 1000)]
    public int MaxFilesPerTask { get; set; } = 20;
}
