using System.ComponentModel.DataAnnotations;

namespace TaskTracker.Api.Dtos;

public record CreateProjectRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: StringLength(2000)] string? Description,
    [property: Range(1, int.MaxValue)] int OwnerId);

public record ProjectResponse(
    int Id,
    string Name,
    string? Description,
    int OwnerId,
    string OwnerName,
    DateTime CreatedAt);
