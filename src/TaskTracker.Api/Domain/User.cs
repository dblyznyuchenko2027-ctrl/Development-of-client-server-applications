namespace TaskTracker.Api.Domain;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Member;

    public ICollection<Project> OwnedProjects { get; set; } = new List<Project>();

    public ICollection<ProjectMember> Memberships { get; set; } = new List<ProjectMember>();

    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
