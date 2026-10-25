using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Infrastructure.OpenApi;
using TaskTracker.Api.Services;

namespace TaskTracker.Api.Controllers;

/// <summary>Users of the system.</summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController(IUserService service) : ControllerBase
{
    /// <summary>List users (paged; filters <c>search</c>, <c>role</c>; sort fields <c>id, email, displayName, role, createdAt</c>).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<UserResponse>), StatusCodes.Status200OK)]
    [ProducesBadRequest]
    public async Task<ActionResult<PagedResponse<UserResponse>>> List([FromQuery] UserListQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));

    /// <summary>Get a user.</summary>
    /// <param name="id">User id.</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesNotFound]
    public async Task<ActionResult<UserResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>Create a user. The e-mail must be unique (409 otherwise); the default role is <c>Member</c>.</summary>
    /// <response code="201">Created; <c>Location</c> points to the new user.</response>
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesBadRequest]
    [ProducesConflict]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Replace a user (full update).</summary>
    /// <param name="id">User id.</param>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesBadRequest]
    [ProducesNotFound]
    [ProducesConflict]
    public async Task<ActionResult<UserResponse>> Update(int id, UpdateUserRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Delete a user. Owners of projects and authors of comments cannot be deleted (409).</summary>
    /// <param name="id">User id.</param>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesNotFound]
    [ProducesConflict]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
