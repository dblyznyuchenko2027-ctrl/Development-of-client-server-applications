namespace TaskTracker.Api.Domain;

/// <summary>File metadata. The upload logic itself is implemented at stage 3.</summary>
public class Attachment : BaseEntity
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public int TaskItemId { get; set; }

    public TaskItem TaskItem { get; set; } = null!;
}
