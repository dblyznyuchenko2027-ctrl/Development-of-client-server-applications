using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public interface IRepository<T> where T : BaseEntity
{
    /// <summary>Returns a tracked entity (use for update/delete).</summary>
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<bool> ExistsAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    void Update(T entity);

    void Remove(T entity);
}
