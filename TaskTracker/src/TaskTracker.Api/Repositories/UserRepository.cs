using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Common;
using TaskTracker.Api.Data;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public class UserRepository(AppDbContext db) : Repository<User>(db), IUserRepository
{
    private static readonly SortMap<User> Sorting = new SortMap<User>(new SortField("id", false), u => u.Id)
        .Add("id", u => u.Id)
        .Add("email", u => u.Email)
        .Add("displayName", u => u.DisplayName)
        .Add("role", u => u.Role)
        .Add("createdAt", u => u.CreatedAt);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, int? exceptUserId, CancellationToken ct = default) =>
        Set.AnyAsync(u => u.Email == email && (exceptUserId == null || u.Id != exceptUserId), ct);

    public Task<PagedList<User>> SearchAsync(
        UserFilter filter,
        IReadOnlyList<SortField> sort,
        PageRequest page,
        CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (filter.Role is { } role)
        {
            query = query.Where(u => u.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = SqlLike.Contains(filter.Search.Trim());
            query = query.Where(u => EF.Functions.ILike(u.Email, pattern) || EF.Functions.ILike(u.DisplayName, pattern));
        }

        return query.ToPagedListAsync(Sorting, sort, page, ct: ct);
    }

    public Task<bool> OwnsProjectsAsync(int userId, CancellationToken ct = default) =>
        Db.Projects.AnyAsync(p => p.OwnerId == userId, ct);

    public Task<bool> HasCommentsAsync(int userId, CancellationToken ct = default) =>
        Db.Comments.AnyAsync(c => c.AuthorId == userId, ct);
}
