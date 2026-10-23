using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Services;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api")]
public class TasksController(ITaskService service) : ControllerBase
{
    /// <summary>GET /api/tasks?status=&amp;priority=&amp;assigneeId=&amp;projectId=&amp;search=&amp;dueFrom=&amp;dueTo=&amp;page=&amp;pageSize=&amp;sort=</summary>
    [HttpGet("tasks")]
    public async Task<ActionResult<PagedResponse<TaskResponse>>> List([FromQuery] TaskListQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, null, ct));

    /// <summary>Same filters as <see cref="List"/>, scoped to one project (404 if the project does not exist).</summary>
    [HttpGet("projects/{projectId:int}/tasks")]
    public async Task<ActionResult<PagedResponse<TaskResponse>>> ListByProject(
        int projectId,
        [FromQuery] TaskListQuery query,
        CancellationToken ct) =>
        Ok(await service.ListAsync(query, projectId, ct));

    [HttpGet("tasks/{id:int}")]
    public async Task<ActionResult<TaskDetailResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost("projects/{projectId:int}/tasks")]
    public async Task<ActionResult<TaskDetailResponse>> Create(int projectId, CreateTaskRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(projectId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("tasks/{id:int}")]
    public async Task<ActionResult<TaskDetailResponse>> Update(int id, UpdateTaskRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    [HttpPatch("tasks/{id:int}/status")]
    public async Task<ActionResult<TaskDetailResponse>> UpdateStatus(int id, UpdateTaskStatusRequest request, CancellationToken ct) =>
        Ok(await service.UpdateStatusAsync(id, request, ct));

    [HttpDelete("tasks/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
