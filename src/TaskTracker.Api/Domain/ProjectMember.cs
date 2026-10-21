namespace TaskTracker.Api.Domain;

/// <summary>Join entity for the many-to-many relation User &lt;-&gt; Project.</summary>
public class ProjectMember
{
    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
