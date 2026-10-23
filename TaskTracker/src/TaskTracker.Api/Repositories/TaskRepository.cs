using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Common;
using TaskTracker.Api.Data;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public class TaskRepository(AppDbContext db) : Repository<TaskItem>(db), ITaskRepository
{
    // Status and priority are stored as strings, so they are sorted by an explicit rank, not alphabetically.
    private static readonly SortMap<TaskItem> Sorting = new SortMap<TaskItem>(new SortField("id", false), t => t.Id)
        .Add("id", t => t.Id)
        .Add("title", t => t.Title)
        .Add("status", t => t.Status == TaskItemStatus.Todo ? 0 : t.Status == TaskItemStatus.InProgress ? 1 : 2)
        .Add("priority", t => t.Priority == TaskPriority.Low ? 0 : t.Priority == TaskPriority.Medium ? 1 : 2)
        .Add("dueDate", t => t.DueDate)
        .Add("createdAt", t => t.CreatedAt)
        .Add("updatedAt", t => t.UpdatedAt)
        .Add("project", t => t.Project.Name)
        .Add("assignee", t => t.Assignee!.DisplayName);

    public Task<PagedList<TaskItem>> SearchAsync(
        TaskFilter filter,
        IReadOnlyList<SortField> sort,
        PageRequest page,
        CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (filter.ProjectId is { } projectId)
        {
            query = query.Where(t => t.ProjectId == projectId);
        }

        if (filter.AssigneeId is { } assigneeId)
        {
            query = query.Where(t => t.AssigneeId == assigneeId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (filter.Priority is { } priority)
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (filter.DueFrom is { } dueFrom)
        {
            query = query.Where(t => t.DueDate >= dueFrom);
        }

        if (filter.DueTo is { } dueTo)
        {
            query = query.Where(t => t.DueDate <= dueTo);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = SqlLike.Contains(filter.Search.Trim());
            query = query.Where(t =>
                EF.Functions.ILike(t.Title, pattern) ||
                (t.Description != null && EF.Functions.ILike(t.Description, pattern)));
        }

        // Eager loading of the to-one relations: the page needs ProjectName and Assignee, so they are joined
        // into the same SELECT instead of being fetched one task at a time (N+1).
        return query.ToPagedListAsync(
            Sorting,
            sort,
            page,
            include: q => q.Include(t => t.Project).Include(t => t.Assignee),
            ct: ct);
    }

    public Task<TaskItem?> GetDetailsAsync(int id, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .AsSplitQuery()
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)).ThenInclude(c => c.Author)
            .Include(t => t.Attachments.OrderBy(a => a.Id))
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> HasAssignedTasksAsync(int projectId, int userId, CancellationToken ct = default) =>
        Set.AnyAsync(t => t.ProjectId == projectId && t.AssigneeId == userId, ct);
}
