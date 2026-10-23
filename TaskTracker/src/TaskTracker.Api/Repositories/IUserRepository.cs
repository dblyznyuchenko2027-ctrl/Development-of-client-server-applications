using TaskTracker.Api.Common;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(string email, int? exceptUserId, CancellationToken ct = default);

    Task<PagedList<User>> SearchAsync(
        UserFilter filter,
        IReadOnlyList<SortField> sort,
        PageRequest page,
        CancellationToken ct = default);

    Task<bool> OwnsProjectsAsync(int userId, CancellationToken ct = default);

    Task<bool> HasCommentsAsync(int userId, CancellationToken ct = default);
}
