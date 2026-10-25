using TaskTracker.Api.Common;
using TaskTracker.Api.Domain;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Infrastructure.Storage;
using TaskTracker.Api.Repositories;

namespace TaskTracker.Api.Services;

public class TaskService(
    ITaskRepository tasks,
    IProjectRepository projects,
    IUserRepository users,
    IAttachmentRepository attachments,
    IFileStorage storage,
    IUnitOfWork unitOfWork) : ITaskService
{
    public async Task<PagedResponse<TaskResponse>> ListAsync(TaskListQuery query, int? projectId, CancellationToken ct = default)
    {
        if (query.DueFrom is { } dueFrom && query.DueTo is { } dueTo && dueFrom > dueTo)
        {
            throw new ValidationFailedException("dueFrom", "dueFrom must not be later than dueTo.");
        }

        if (projectId is { } routeProjectId && !await projects.ExistsAsync(routeProjectId, ct))
        {
            throw new NotFoundException("Project", routeProjectId);
        }

        var page = query.ToPageRequest();
        var filter = new TaskFilter(
            query.Status,
            query.Priority,
            query.AssigneeId,
            projectId ?? query.ProjectId,
            query.Search,
            query.DueFrom,
            query.DueTo);

        var result = await tasks.SearchAsync(filter, query.ParseSort(), page, ct);
        return result.ToResponse(page, t => t.ToResponse());
    }

    public async Task<TaskDetailResponse> GetAsync(int id, CancellationToken ct = default)
    {
        var task = await tasks.GetDetailsAsync(id, ct) ?? throw new NotFoundException("Task", id);
        return task.ToDetailResponse();
    }

    public async Task<TaskDetailResponse> CreateAsync(int projectId, CreateTaskRequest request, CancellationToken ct = default)
    {
        if (!await projects.ExistsAsync(projectId, ct))
        {
            throw new NotFoundException("Project", projectId);
        }

        await EnsureAssigneeAllowedAsync(projectId, request.AssigneeId, ct);

        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description.NullIfBlank(),
            Status = request.Status ?? TaskItemStatus.Todo,
            Priority = request.Priority ?? TaskPriority.Medium,
            DueDate = request.DueDate,
            ProjectId = projectId,
            AssigneeId = request.AssigneeId
        };

        await tasks.AddAsync(task, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(task.Id, ct);
    }

    public async Task<TaskDetailResponse> UpdateAsync(int id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task", id);

        await EnsureAssigneeAllowedAsync(task.ProjectId, request.AssigneeId, ct);

        task.Title = request.Title.Trim();
        task.Description = request.Description.NullIfBlank();
        task.Status = request.Status!.Value;
        task.Priority = request.Priority!.Value;
        task.DueDate = request.DueDate;
        task.AssigneeId = request.AssigneeId;
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<TaskDetailResponse> UpdateStatusAsync(int id, UpdateTaskStatusRequest request, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task", id);

        task.Status = request.Status!.Value;
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task", id);

        var filePaths = await attachments.ListStoragePathsByTaskAsync(id, ct);

        // Attachment rows are removed by ON DELETE CASCADE; the files on disk are removed after the commit.
        tasks.Remove(task);
        await unitOfWork.SaveChangesAsync(ct);

        foreach (var path in filePaths)
        {
            storage.Delete(path);
        }
    }

    /// <summary>The assignee must exist (400) and be a member of the task's project (422).</summary>
    private async Task EnsureAssigneeAllowedAsync(int projectId, int? assigneeId, CancellationToken ct)
    {
        if (assigneeId is not { } userId)
        {
            return;
        }

        if (!await users.ExistsAsync(userId, ct))
        {
            throw new ValidationFailedException("assigneeId", $"User with id {userId} does not exist.");
        }

        if (!await projects.IsMemberAsync(projectId, userId, ct))
        {
            throw new BusinessRuleException($"User {userId} is not a member of project {projectId} and cannot be assigned its tasks.");
        }
    }
}
