namespace TaskTracker.Api.Domain;

/// <summary>Metadata of a file attached to a task; the bytes live in <c>IFileStorage</c> under <see cref="StoragePath"/>.</summary>
public class Attachment : BaseEntity
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public int TaskItemId { get; set; }

    public TaskItem TaskItem { get; set; } = null!;
}
