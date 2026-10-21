using TaskTracker.Api.Dtos;

namespace TaskTracker.Api.Services;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectResponse>> GetAllAsync(CancellationToken ct = default);

    Task<ProjectResponse?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns null when the owner does not exist.</summary>
    Task<ProjectResponse?> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
}
