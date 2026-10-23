using TaskTracker.Api.Common;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public interface ITaskRepository : IRepository<TaskItem>
{
    /// <summary>Filtered, sorted page of tasks with project and assignee loaded in the same query.</summary>
    Task<PagedList<TaskItem>> SearchAsync(
        TaskFilter filter,
        IReadOnlyList<SortField> sort,
        PageRequest page,
        CancellationToken ct = default);

    /// <summary>Read-only task with project, assignee, comments (+authors) and attachments loaded eagerly.</summary>
    Task<TaskItem?> GetDetailsAsync(int id, CancellationToken ct = default);

    Task<bool> HasAssignedTasksAsync(int projectId, int userId, CancellationToken ct = default);
}
