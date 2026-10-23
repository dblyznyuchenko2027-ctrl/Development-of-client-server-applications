namespace TaskTracker.Api.Domain;

public enum UserRole
{
    Admin,
    ProjectManager,
    Member
}

public enum TaskItemStatus
{
    Todo,
    InProgress,
    Done
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}
