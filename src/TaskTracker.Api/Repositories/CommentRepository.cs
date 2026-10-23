using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Common;
using TaskTracker.Api.Data;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public class CommentRepository(AppDbContext db) : Repository<Comment>(db), ICommentRepository
{
    private static readonly SortMap<Comment> Sorting = new SortMap<Comment>(new SortField("createdAt", false), c => c.Id)
        .Add("id", c => c.Id)
        .Add("createdAt", c => c.CreatedAt);

    public Task<PagedList<Comment>> SearchByTaskAsync(
        int taskId,
        IReadOnlyList<SortField> sort,
        PageRequest page,
        CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Where(c => c.TaskItemId == taskId)
            .ToPagedListAsync(Sorting, sort, page, include: q => q.Include(c => c.Author), ct: ct);

    public Task<Comment?> GetWithAuthorAsync(int id, CancellationToken ct = default) =>
        Set.AsNoTracking().Include(c => c.Author).FirstOrDefaultAsync(c => c.Id == id, ct);
}
