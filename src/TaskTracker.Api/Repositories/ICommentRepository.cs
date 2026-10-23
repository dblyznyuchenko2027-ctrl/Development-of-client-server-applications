using TaskTracker.Api.Common;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public interface ICommentRepository : IRepository<Comment>
{
    Task<PagedList<Comment>> SearchByTaskAsync(
        int taskId,
        IReadOnlyList<SortField> sort,
        PageRequest page,
        CancellationToken ct = default);

    /// <summary>Read-only comment with its author.</summary>
    Task<Comment?> GetWithAuthorAsync(int id, CancellationToken ct = default);
}
