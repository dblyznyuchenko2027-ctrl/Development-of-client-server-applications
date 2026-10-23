using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;

namespace TaskTracker.Api.Services;

public interface IProjectService
{
    Task<PagedResponse<ProjectResponse>> ListAsync(ProjectListQuery query, CancellationToken ct = default);

    /// <exception cref="NotFoundException">Project does not exist.</exception>
    Task<ProjectDetailResponse> GetAsync(int id, CancellationToken ct = default);

    /// <exception cref="ValidationFailedException">Owner does not exist.</exception>
    Task<ProjectDetailResponse> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);

    Task<ProjectDetailResponse> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Idempotent: adding an existing member is a no-op.</summary>
    Task AddMemberAsync(int projectId, int userId, CancellationToken ct = default);

    Task RemoveMemberAsync(int projectId, int userId, CancellationToken ct = default);
}
