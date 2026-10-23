using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Services;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(IProjectService service) : ControllerBase
{
    /// <summary>GET /api/projects?search=&amp;ownerId=&amp;page=&amp;pageSize=&amp;sort=-createdAt</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProjectResponse>>> List([FromQuery] ProjectListQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDetailResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ProjectDetailResponse>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProjectDetailResponse>> Update(int id, UpdateProjectRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:int}/members/{userId:int}")]
    public async Task<IActionResult> AddMember(int id, int userId, CancellationToken ct)
    {
        await service.AddMemberAsync(id, userId, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/members/{userId:int}")]
    public async Task<IActionResult> RemoveMember(int id, int userId, CancellationToken ct)
    {
        await service.RemoveMemberAsync(id, userId, ct);
        return NoContent();
    }
}
