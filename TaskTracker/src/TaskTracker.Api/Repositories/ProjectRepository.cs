using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Common;
using TaskTracker.Api.Data;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public class ProjectRepository(AppDbContext db) : Repository<Project>(db), IProjectRepository
{
    private static readonly SortMap<Project> Sorting = new SortMap<Project>(new SortField("id", false), p => p.Id)
        .Add("id", p => p.Id)
        .Add("name", p => p.Name)
        .Add("createdAt", p => p.CreatedAt)
        .Add("owner", p => p.Owner.DisplayName)
        .Add("tasksCount", p => p.Tasks.Count)
        .Add("membersCount", p => p.Members.Count);

    public async Task<PagedList<ProjectListItem>> SearchAsync(
        ProjectFilter filter,
        IReadOnlyList<SortField> sort,
        PageRequest page,
        CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (filter.OwnerId is { } ownerId)
        {
            query = query.Where(p => p.OwnerId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = SqlLike.Contains(filter.Search.Trim());
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                (p.Description != null && EF.Functions.ILike(p.Description, pattern)));
        }

        var total = await query.CountAsync(ct);

        // Projection: owner name and both counters come from the same SELECT (joins / correlated subqueries).
        var items = await Sorting.Apply(query, sort)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(p => new ProjectListItem(
                p.Id,
                p.Name,
                p.Description,
                p.OwnerId,
                p.Owner.DisplayName,
                p.Tasks.Count,
                p.Members.Count,
                p.CreatedAt))
            .ToListAsync(ct);

        return new PagedList<ProjectListItem>(items, total);
    }

    public Task<Project?> GetDetailsAsync(int id, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Owner)
            .Include(p => p.Members.OrderBy(m => m.JoinedAt)).ThenInclude(m => m.User)
            .Include(p => p.Tasks.OrderBy(t => t.Id)).ThenInclude(t => t.Assignee)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> IsMemberAsync(int projectId, int userId, CancellationToken ct = default) =>
        Db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);

    public Task<ProjectMember?> GetMemberAsync(int projectId, int userId, CancellationToken ct = default) =>
        Db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);

    public void AddMember(ProjectMember member) => Db.ProjectMembers.Add(member);

    public void RemoveMember(ProjectMember member) => Db.ProjectMembers.Remove(member);
}
