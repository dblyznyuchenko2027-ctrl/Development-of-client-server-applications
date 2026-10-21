using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public interface IProjectRepository : IRepository<Project>
{
    Task<Project?> GetWithOwnerAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Project>> ListWithOwnerAsync(CancellationToken ct = default);
}
