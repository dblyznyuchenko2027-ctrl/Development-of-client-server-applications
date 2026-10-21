using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Services;

namespace TaskTracker.Api.Controllers;

/// <summary>Stage 1: read + create only, to demonstrate the controller -> service -> repository chain.</summary>
[ApiController]
[Route("api/projects")]
public class ProjectsController(IProjectService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> GetAll(CancellationToken ct) =>
        Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectResponse>> GetById(int id, CancellationToken ct)
    {
        var project = await service.GetByIdAsync(id, ct);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        if (created is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Owner not found",
                Detail = $"User with id {request.OwnerId} does not exist."
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
