using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public sealed record ProjectFilter(string? Search, int? OwnerId);

public sealed record TaskFilter(
    TaskItemStatus? Status,
    TaskPriority? Priority,
    int? AssigneeId,
    int? ProjectId,
    string? Search,
    DateOnly? DueFrom,
    DateOnly? DueTo);

public sealed record UserFilter(string? Search, UserRole? Role);

/// <summary>Projection used by the projects list: counters are computed in SQL, no collections are loaded.</summary>
public sealed record ProjectListItem(
    int Id,
    string Name,
    string? Description,
    int OwnerId,
    string OwnerName,
    int TasksCount,
    int MembersCount,
    DateTime CreatedAt);
