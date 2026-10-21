using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public interface ITaskRepository : IRepository<TaskItem>
{
    Task<IReadOnlyList<TaskItem>> ListByProjectAsync(int projectId, CancellationToken ct = default);
}
