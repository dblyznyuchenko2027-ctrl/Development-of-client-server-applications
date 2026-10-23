using System.ComponentModel.DataAnnotations;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Dtos;

public record CreateTaskRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Title,
    [property: StringLength(4000)] string? Description,
    [property: EnumDataType(typeof(TaskItemStatus))] TaskItemStatus? Status,
    [property: EnumDataType(typeof(TaskPriority))] TaskPriority? Priority,
    DateOnly? DueDate,
    [property: Range(1, int.MaxValue)] int? AssigneeId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DueDate is { } due && due < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            yield return new ValidationResult("Due date cannot be in the past.", new[] { nameof(DueDate) });
        }
    }
}

/// <summary>PUT body: full replacement, so Status and Priority are mandatory; null AssigneeId = unassign.</summary>
public record UpdateTaskRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Title,
    [property: StringLength(4000)] string? Description,
    [property: Required, EnumDataType(typeof(TaskItemStatus))] TaskItemStatus? Status,
    [property: Required, EnumDataType(typeof(TaskPriority))] TaskPriority? Priority,
    DateOnly? DueDate,
    [property: Range(1, int.MaxValue)] int? AssigneeId);

public record UpdateTaskStatusRequest(
    [property: Required, EnumDataType(typeof(TaskItemStatus))] TaskItemStatus? Status);

/// <summary>Compact task used inside a project.</summary>
public record TaskSummaryResponse(
    int Id,
    string Title,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    UserSummary? Assignee);

/// <summary>Collection item: task with its project name and assignee (to-one relations only).</summary>
public record TaskResponse(
    int Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    int ProjectId,
    string ProjectName,
    UserSummary? Assignee,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Single-resource representation with nested comments and attachments.</summary>
public record TaskDetailResponse(
    int Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    int ProjectId,
    string ProjectName,
    UserSummary? Assignee,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<CommentResponse> Comments,
    IReadOnlyList<AttachmentResponse> Attachments);

public record AttachmentResponse(int Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt);

public class TaskListQuery : PageQuery
{
    [EnumDataType(typeof(TaskItemStatus))]
    public TaskItemStatus? Status { get; init; }

    [EnumDataType(typeof(TaskPriority))]
    public TaskPriority? Priority { get; init; }

    [Range(1, int.MaxValue)]
    public int? AssigneeId { get; init; }

    /// <summary>Ignored on <c>/api/projects/{projectId}/tasks</c>, where the route value wins.</summary>
    [Range(1, int.MaxValue)]
    public int? ProjectId { get; init; }

    /// <summary>Case-insensitive substring of title or description.</summary>
    [StringLength(100)]
    public string? Search { get; init; }

    public DateOnly? DueFrom { get; init; }

    public DateOnly? DueTo { get; init; }
}
