using TaskTracker.Api.Domain;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Repositories;

namespace TaskTracker.Api.Services;

/// <summary>
/// Entity -> DTO mapping. Methods that touch navigation properties rely on the repository having loaded them
/// (see the Include calls in the repositories), so no lazy loading / per-row queries happen here.
/// </summary>
public static class Mapping
{
    public static UserSummary ToSummary(this User user) => new(user.Id, user.DisplayName);

    public static UserResponse ToResponse(this User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAt);

    public static ProjectResponse ToResponse(this ProjectListItem p) =>
        new(p.Id, p.Name, p.Description, p.OwnerId, p.OwnerName, p.TasksCount, p.MembersCount, p.CreatedAt);

    public static ProjectDetailResponse ToDetailResponse(this Project p) =>
        new(
            p.Id,
            p.Name,
            p.Description,
            p.Owner.ToSummary(),
            p.Members.Select(m => new MemberResponse(m.UserId, m.User.DisplayName, m.User.Email, m.JoinedAt)).ToList(),
            p.Tasks.Select(t => t.ToSummary()).ToList(),
            p.CreatedAt);

    public static TaskSummaryResponse ToSummary(this TaskItem t) =>
        new(t.Id, t.Title, t.Status, t.Priority, t.DueDate, t.Assignee?.ToSummary());

    public static TaskResponse ToResponse(this TaskItem t) =>
        new(
            t.Id,
            t.Title,
            t.Description,
            t.Status,
            t.Priority,
            t.DueDate,
            t.ProjectId,
            t.Project.Name,
            t.Assignee?.ToSummary(),
            t.CreatedAt,
            t.UpdatedAt);

    public static TaskDetailResponse ToDetailResponse(this TaskItem t) =>
        new(
            t.Id,
            t.Title,
            t.Description,
            t.Status,
            t.Priority,
            t.DueDate,
            t.ProjectId,
            t.Project.Name,
            t.Assignee?.ToSummary(),
            t.CreatedAt,
            t.UpdatedAt,
            t.Comments.Select(c => c.ToResponse()).ToList(),
            t.Attachments.Select(a => new AttachmentResponse(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt)).ToList());

    public static CommentResponse ToResponse(this Comment c) =>
        new(c.Id, c.Text, c.TaskItemId, c.Author.ToSummary(), c.CreatedAt);
}
