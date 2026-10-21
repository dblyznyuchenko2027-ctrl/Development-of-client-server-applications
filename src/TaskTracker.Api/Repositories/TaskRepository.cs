using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Data;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public class TaskRepository(AppDbContext db) : Repository<TaskItem>(db), ITaskRepository
{
    public async Task<IReadOnlyList<TaskItem>> ListByProjectAsync(int projectId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(t => t.ProjectId == projectId).OrderBy(t => t.Id).ToListAsync(ct);
}
