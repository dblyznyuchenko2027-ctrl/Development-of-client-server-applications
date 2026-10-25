using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Data;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public class AttachmentRepository(AppDbContext db) : Repository<Attachment>(db), IAttachmentRepository
{
    public async Task<IReadOnlyList<Attachment>> ListByTaskAsync(int taskId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(a => a.TaskItemId == taskId)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

    public Task<int> CountByTaskAsync(int taskId, CancellationToken ct = default) =>
        Set.CountAsync(a => a.TaskItemId == taskId, ct);

    public async Task<IReadOnlyList<string>> ListStoragePathsByTaskAsync(int taskId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(a => a.TaskItemId == taskId)
            .Select(a => a.StoragePath)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListStoragePathsByProjectAsync(int projectId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(a => a.TaskItem.ProjectId == projectId)
            .Select(a => a.StoragePath)
            .ToListAsync(ct);
}
