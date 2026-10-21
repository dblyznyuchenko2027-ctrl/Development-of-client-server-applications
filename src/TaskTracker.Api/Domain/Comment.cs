namespace TaskTracker.Api.Domain;

public class Comment : BaseEntity
{
    public string Text { get; set; } = string.Empty;

    public int TaskItemId { get; set; }

    public TaskItem TaskItem { get; set; } = null!;

    public int AuthorId { get; set; }

    public User Author { get; set; } = null!;
}
