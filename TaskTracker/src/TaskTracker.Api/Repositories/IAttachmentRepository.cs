using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public interface IAttachmentRepository : IRepository<Attachment>
{
    Task<IReadOnlyList<Attachment>> ListByTaskAsync(int taskId, CancellationToken ct = default);

    Task<int> CountByTaskAsync(int taskId, CancellationToken ct = default);

    /// <summary>Storage paths of all files of a task (used to clean up the disk after the task is deleted).</summary>
    Task<IReadOnlyList<string>> ListStoragePathsByTaskAsync(int taskId, CancellationToken ct = default);

    /// <summary>Storage paths of all files of all tasks of a project.</summary>
    Task<IReadOnlyList<string>> ListStoragePathsByProjectAsync(int projectId, CancellationToken ct = default);
}
