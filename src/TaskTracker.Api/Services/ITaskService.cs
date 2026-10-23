using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;

namespace TaskTracker.Api.Services;

public interface ITaskService
{
    /// <param name="query">Filters, paging and sorting.</param>
    /// <param name="projectId">When set (nested route), overrides <c>query.ProjectId</c> and must exist.</param>
    Task<PagedResponse<TaskResponse>> ListAsync(TaskListQuery query, int? projectId, CancellationToken ct = default);

    Task<TaskDetailResponse> GetAsync(int id, CancellationToken ct = default);

    Task<TaskDetailResponse> CreateAsync(int projectId, CreateTaskRequest request, CancellationToken ct = default);

    Task<TaskDetailResponse> UpdateAsync(int id, UpdateTaskRequest request, CancellationToken ct = default);

    Task<TaskDetailResponse> UpdateStatusAsync(int id, UpdateTaskStatusRequest request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
