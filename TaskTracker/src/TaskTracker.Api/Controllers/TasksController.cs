using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Infrastructure.OpenApi;
using TaskTracker.Api.Services;

namespace TaskTracker.Api.Controllers;

/// <summary>Tasks: CRUD, filtering across projects, status changes.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class TasksController(ITaskService service) : ControllerBase
{
    /// <summary>List tasks of all projects (paged, filterable, sortable).</summary>
    /// <remarks>
    /// Filters: <c>status, priority, assigneeId, projectId, search, dueFrom, dueTo</c>.
    /// Sort fields: <c>id, title, status, priority, dueDate, createdAt, updatedAt, project, assignee</c>
    /// (prefix <c>-</c> = descending). <c>status</c> and <c>priority</c> are ordered by meaning, not alphabetically.
    /// </remarks>
    [HttpGet("tasks")]
    [ProducesResponseType(typeof(PagedResponse<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesBadRequest]
    public async Task<ActionResult<PagedResponse<TaskResponse>>> List([FromQuery] TaskListQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, null, ct));

    /// <summary>List tasks of one project (same filters as <c>GET /api/tasks</c>).</summary>
    /// <param name="projectId">Project id; wins over the <c>projectId</c> query parameter.</param>
    [HttpGet("projects/{projectId:int}/tasks")]
    [ProducesResponseType(typeof(PagedResponse<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesBadRequest]
    [ProducesNotFound]
    public async Task<ActionResult<PagedResponse<TaskResponse>>> ListByProject(
        int projectId,
        [FromQuery] TaskListQuery query,
        CancellationToken ct) =>
        Ok(await service.ListAsync(query, projectId, ct));

    /// <summary>Get a task with its comments and attachments.</summary>
    /// <param name="id">Task id.</param>
    [HttpGet("tasks/{id:int}")]
    [ProducesResponseType(typeof(TaskDetailResponse), StatusCodes.Status200OK)]
    [ProducesNotFound]
    public async Task<ActionResult<TaskDetailResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>Create a task in a project.</summary>
    /// <remarks>The assignee, if given, must be a member of the project (422 otherwise). The due date cannot be in the past.</remarks>
    /// <param name="projectId">Project id.</param>
    /// <response code="201">Created; <c>Location</c> points to the new task.</response>
    [HttpPost("projects/{projectId:int}/tasks")]
    [ProducesResponseType(typeof(TaskDetailResponse), StatusCodes.Status201Created)]
    [ProducesBadRequest]
    [ProducesNotFound]
    [ProducesUnprocessable]
    public async Task<ActionResult<TaskDetailResponse>> Create(int projectId, CreateTaskRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(projectId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Replace a task (full update). A null <c>assigneeId</c> unassigns the task.</summary>
    /// <param name="id">Task id.</param>
    [HttpPut("tasks/{id:int}")]
    [ProducesResponseType(typeof(TaskDetailResponse), StatusCodes.Status200OK)]
    [ProducesBadRequest]
    [ProducesNotFound]
    [ProducesUnprocessable]
    public async Task<ActionResult<TaskDetailResponse>> Update(int id, UpdateTaskRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Change only the status of a task.</summary>
    /// <param name="id">Task id.</param>
    [HttpPatch("tasks/{id:int}/status")]
    [ProducesResponseType(typeof(TaskDetailResponse), StatusCodes.Status200OK)]
    [ProducesBadRequest]
    [ProducesNotFound]
    public async Task<ActionResult<TaskDetailResponse>> UpdateStatus(int id, UpdateTaskStatusRequest request, CancellationToken ct) =>
        Ok(await service.UpdateStatusAsync(id, request, ct));

    /// <summary>Delete a task together with its comments and attachments (including the stored files).</summary>
    /// <param name="id">Task id.</param>
    [HttpDelete("tasks/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesNotFound]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
