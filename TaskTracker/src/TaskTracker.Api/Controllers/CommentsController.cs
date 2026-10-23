using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Services;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api")]
public class CommentsController(ICommentService service) : ControllerBase
{
    [HttpGet("tasks/{taskId:int}/comments")]
    public async Task<ActionResult<PagedResponse<CommentResponse>>> ListByTask(
        int taskId,
        [FromQuery] CommentListQuery query,
        CancellationToken ct) =>
        Ok(await service.ListAsync(taskId, query, ct));

    [HttpGet("comments/{id:int}")]
    public async Task<ActionResult<CommentResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost("tasks/{taskId:int}/comments")]
    public async Task<ActionResult<CommentResponse>> Create(int taskId, CreateCommentRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(taskId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("comments/{id:int}")]
    public async Task<ActionResult<CommentResponse>> Update(int id, UpdateCommentRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    [HttpDelete("comments/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
