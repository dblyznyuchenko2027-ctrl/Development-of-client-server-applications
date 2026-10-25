using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Infrastructure.OpenApi;
using TaskTracker.Api.Services;

namespace TaskTracker.Api.Controllers;

/// <summary>Comments on tasks.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class CommentsController(ICommentService service) : ControllerBase
{
    /// <summary>List comments of a task (paged; sort fields: <c>id, createdAt</c>).</summary>
    /// <param name="taskId">Task id.</param>
    [HttpGet("tasks/{taskId:int}/comments")]
    [ProducesResponseType(typeof(PagedResponse<CommentResponse>), StatusCodes.Status200OK)]
    [ProducesBadRequest]
    [ProducesNotFound]
    public async Task<ActionResult<PagedResponse<CommentResponse>>> ListByTask(
        int taskId,
        [FromQuery] CommentListQuery query,
        CancellationToken ct) =>
        Ok(await service.ListAsync(taskId, query, ct));

    /// <summary>Get a comment with its author.</summary>
    /// <param name="id">Comment id.</param>
    [HttpGet("comments/{id:int}")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status200OK)]
    [ProducesNotFound]
    public async Task<ActionResult<CommentResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>Add a comment to a task.</summary>
    /// <param name="taskId">Task id.</param>
    /// <response code="201">Created; <c>Location</c> points to the new comment.</response>
    [HttpPost("tasks/{taskId:int}/comments")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
    [ProducesBadRequest]
    [ProducesNotFound]
    public async Task<ActionResult<CommentResponse>> Create(int taskId, CreateCommentRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(taskId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Change the text of a comment.</summary>
    /// <param name="id">Comment id.</param>
    [HttpPut("comments/{id:int}")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status200OK)]
    [ProducesBadRequest]
    [ProducesNotFound]
    public async Task<ActionResult<CommentResponse>> Update(int id, UpdateCommentRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Delete a comment.</summary>
    /// <param name="id">Comment id.</param>
    [HttpDelete("comments/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesNotFound]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
