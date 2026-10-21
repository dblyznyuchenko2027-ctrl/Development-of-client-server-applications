namespace TaskTracker.Api.Domain;

public class TaskItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateOnly? DueDate { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public int? AssigneeId { get; set; }

    public User? Assignee { get; set; }

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
