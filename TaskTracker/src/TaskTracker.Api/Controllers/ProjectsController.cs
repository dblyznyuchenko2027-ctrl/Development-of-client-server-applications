using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Infrastructure.OpenApi;
using TaskTracker.Api.Services;

namespace TaskTracker.Api.Controllers;

/// <summary>Projects and their members.</summary>
[ApiController]
[Route("api/projects")]
[Produces("application/json")]
public class ProjectsController(IProjectService service) : ControllerBase
{
    /// <summary>List projects (paged, filterable, sortable).</summary>
    /// <remarks>
    /// Sort fields: <c>id, name, createdAt, owner, tasksCount, membersCount</c>; prefix <c>-</c> = descending
    /// (e.g. <c>sort=-createdAt,name</c>). Each item contains counters instead of nested collections.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ProjectResponse>), StatusCodes.Status200OK)]
    [ProducesBadRequest]
    public async Task<ActionResult<PagedResponse<ProjectResponse>>> List([FromQuery] ProjectListQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));

    /// <summary>Get a project with its owner, members and tasks.</summary>
    /// <param name="id">Project id.</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesNotFound]
    public async Task<ActionResult<ProjectDetailResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>Create a project. The owner is automatically added as a member.</summary>
    /// <response code="201">Created; <c>Location</c> points to the new project.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status201Created)]
    [ProducesBadRequest]
    public async Task<ActionResult<ProjectDetailResponse>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Replace the name and description of a project (the owner cannot be changed).</summary>
    /// <param name="id">Project id.</param>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesBadRequest]
    [ProducesNotFound]
    public async Task<ActionResult<ProjectDetailResponse>> Update(int id, UpdateProjectRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Delete a project together with its tasks, comments and attachments (cascade).</summary>
    /// <param name="id">Project id.</param>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesNotFound]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Add a user to the project. Idempotent: adding an existing member is not an error.</summary>
    /// <param name="id">Project id.</param>
    /// <param name="userId">User id.</param>
    [HttpPut("{id:int}/members/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesNotFound]
    public async Task<IActionResult> AddMember(int id, int userId, CancellationToken ct)
    {
        await service.AddMemberAsync(id, userId, ct);
        return NoContent();
    }

    /// <summary>Remove a user from the project.</summary>
    /// <remarks>The owner and users who still have tasks assigned in the project cannot be removed (409).</remarks>
    /// <param name="id">Project id.</param>
    /// <param name="userId">User id.</param>
    [HttpDelete("{id:int}/members/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesNotFound]
    [ProducesConflict]
    public async Task<IActionResult> RemoveMember(int id, int userId, CancellationToken ct)
    {
        await service.RemoveMemberAsync(id, userId, ct);
        return NoContent();
    }
}
