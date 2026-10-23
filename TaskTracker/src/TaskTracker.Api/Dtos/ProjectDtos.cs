using System.ComponentModel.DataAnnotations;

namespace TaskTracker.Api.Dtos;

public record CreateProjectRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: StringLength(2000)] string? Description,
    [property: Range(1, int.MaxValue)] int OwnerId);

/// <summary>PUT body. The owner is immutable at this stage.</summary>
public record UpdateProjectRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: StringLength(2000)] string? Description);

/// <summary>Collection item: flat, with aggregate counters (no nested collections).</summary>
public record ProjectResponse(
    int Id,
    string Name,
    string? Description,
    int OwnerId,
    string OwnerName,
    int TasksCount,
    int MembersCount,
    DateTime CreatedAt);

public record MemberResponse(int UserId, string DisplayName, string Email, DateTime JoinedAt);

/// <summary>Single-resource representation with nested owner, members and tasks.</summary>
public record ProjectDetailResponse(
    int Id,
    string Name,
    string? Description,
    UserSummary Owner,
    IReadOnlyList<MemberResponse> Members,
    IReadOnlyList<TaskSummaryResponse> Tasks,
    DateTime CreatedAt);

public class ProjectListQuery : PageQuery
{
    /// <summary>Case-insensitive substring of name or description.</summary>
    [StringLength(100)]
    public string? Search { get; init; }

    [Range(1, int.MaxValue)]
    public int? OwnerId { get; init; }
}
