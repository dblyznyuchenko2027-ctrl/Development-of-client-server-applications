using System.Net;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Common;
using TaskTracker.Api.Domain;
using TaskTracker.Api.Dtos;
using TaskTracker.Tests.Infrastructure;

namespace TaskTracker.Tests.Api;

/// <summary>Tasks: request -> response -> database state.</summary>
[Collection(ApiCollection.Name)]
public sealed class TasksApiTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Create_task_returns_201_and_persists_it()
    {
        var seed = await SeedProjectAsync();
        var due = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        var response = await PostJsonAsync($"/api/projects/{seed.ProjectId}/tasks", new
        {
            title = "  Write integration tests  ",
            description = "Cover the key endpoints",
            priority = "High",
            dueDate = due,
            assigneeId = seed.MemberId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync<TaskDetailResponse>(response);
        Assert.Equal($"/api/tasks/{body.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("Write integration tests", body.Title);
        Assert.Equal(TaskItemStatus.Todo, body.Status);
        Assert.Equal(TaskPriority.High, body.Priority);
        Assert.Equal(seed.MemberId, body.Assignee?.Id);

        var saved = await WithDbAsync(db => db.Tasks.AsNoTracking().SingleAsync(t => t.Id == body.Id));
        Assert.Equal("Write integration tests", saved.Title);
        Assert.Equal(seed.ProjectId, saved.ProjectId);
        Assert.Equal(seed.MemberId, saved.AssigneeId);
        Assert.Equal(TaskPriority.High, saved.Priority);
        Assert.Equal(due, saved.DueDate);
    }

    [Fact]
    public async Task Create_task_in_unknown_project_returns_404_and_changes_nothing()
    {
        await SeedProjectAsync();

        var response = await PostJsonAsync("/api/projects/9999/tasks", new { title = "Orphan" });

        AssertProblem(response, HttpStatusCode.NotFound);
        Assert.Equal(0, await WithDbAsync(db => db.Tasks.CountAsync()));
    }

    [Fact]
    public async Task Create_task_with_empty_title_returns_400_with_field_errors()
    {
        var seed = await SeedProjectAsync();

        var response = await PostJsonAsync($"/api/projects/{seed.ProjectId}/tasks", new { title = "" });

        AssertProblem(response, HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("title", out _), "errors must contain 'title'");
        Assert.Equal(0, await WithDbAsync(db => db.Tasks.CountAsync()));
    }

    [Fact]
    public async Task Create_task_with_due_date_in_the_past_returns_400()
    {
        var seed = await SeedProjectAsync();

        var response = await PostJsonAsync($"/api/projects/{seed.ProjectId}/tasks", new { title = "Late", dueDate = "2000-01-01" });

        AssertProblem(response, HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("dueDate", out _), "errors must contain 'dueDate'");
        Assert.Equal(0, await WithDbAsync(db => db.Tasks.CountAsync()));
    }

    [Fact]
    public async Task Create_task_for_non_member_assignee_returns_422()
    {
        var seed = await SeedProjectAsync();

        var response = await PostJsonAsync($"/api/projects/{seed.ProjectId}/tasks", new
        {
            title = "Nobody can do it",
            assigneeId = seed.OutsiderId
        });

        AssertProblem(response, HttpStatusCode.UnprocessableEntity);
        Assert.Equal(0, await WithDbAsync(db => db.Tasks.CountAsync()));
    }

    [Fact]
    public async Task Get_task_returns_nested_project_assignee_and_comments()
    {
        var seed = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(seed.ProjectId, "Nested", assigneeId: seed.MemberId);
        var commentResponse = await PostJsonAsync($"/api/tasks/{taskId}/comments", new { text = "First!", authorId = seed.ManagerId });
        Assert.Equal(HttpStatusCode.Created, commentResponse.StatusCode);

        var response = await Client.GetAsync($"/api/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<TaskDetailResponse>(response);
        Assert.Equal("Test project", body.ProjectName);
        Assert.Equal("Member", body.Assignee?.DisplayName);
        var comment = Assert.Single(body.Comments);
        Assert.Equal("First!", comment.Text);
        Assert.Equal("Manager", comment.Author.DisplayName);
        Assert.Empty(body.Attachments);
    }

    [Fact]
    public async Task Get_unknown_task_returns_404_problem()
    {
        var response = await Client.GetAsync("/api/tasks/12345");

        AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_status_changes_status_in_database()
    {
        var seed = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(seed.ProjectId);

        var response = await PatchJsonAsync($"/api/tasks/{taskId}/status", new { status = "Done" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TaskItemStatus.Done, (await ReadAsync<TaskDetailResponse>(response)).Status);
        var saved = await WithDbAsync(db => db.Tasks.AsNoTracking().SingleAsync(t => t.Id == taskId));
        Assert.Equal(TaskItemStatus.Done, saved.Status);
        Assert.NotNull(saved.UpdatedAt);
    }

    [Fact]
    public async Task Patch_status_with_unknown_value_returns_400_and_keeps_status()
    {
        var seed = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(seed.ProjectId);

        var response = await PatchJsonAsync($"/api/tasks/{taskId}/status", new { status = "Blocked" });

        AssertProblem(response, HttpStatusCode.BadRequest);
        var saved = await WithDbAsync(db => db.Tasks.AsNoTracking().SingleAsync(t => t.Id == taskId));
        Assert.Equal(TaskItemStatus.Todo, saved.Status);
    }

    [Fact]
    public async Task Put_replaces_task_and_unassigns_when_assignee_is_null()
    {
        var seed = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(seed.ProjectId, "Old title", assigneeId: seed.MemberId);

        var response = await PutJsonAsync($"/api/tasks/{taskId}", new
        {
            title = "New title",
            description = "New description",
            status = "InProgress",
            priority = "Low",
            assigneeId = (int?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await WithDbAsync(db => db.Tasks.AsNoTracking().SingleAsync(t => t.Id == taskId));
        Assert.Equal("New title", saved.Title);
        Assert.Equal("New description", saved.Description);
        Assert.Equal(TaskItemStatus.InProgress, saved.Status);
        Assert.Equal(TaskPriority.Low, saved.Priority);
        Assert.Null(saved.AssigneeId);
    }

    [Fact]
    public async Task Delete_task_returns_204_removes_row_and_its_comments()
    {
        var seed = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(seed.ProjectId);
        await PostJsonAsync($"/api/tasks/{taskId}/comments", new { text = "bye", authorId = seed.ManagerId });
        Assert.Equal(1, await WithDbAsync(db => db.Comments.CountAsync()));

        var response = await Client.DeleteAsync($"/api/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await WithDbAsync(db => db.Tasks.AnyAsync(t => t.Id == taskId)));
        Assert.Equal(0, await WithDbAsync(db => db.Comments.CountAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/tasks/{taskId}")).StatusCode);
    }

    [Fact]
    public async Task List_filters_sorts_and_pages_in_the_database()
    {
        var seed = await SeedProjectAsync();
        await SeedTaskAsync(seed.ProjectId, "Alpha", TaskItemStatus.Done, TaskPriority.High);
        await SeedTaskAsync(seed.ProjectId, "Bravo", TaskItemStatus.Todo, TaskPriority.Low);
        await SeedTaskAsync(seed.ProjectId, "Charlie", TaskItemStatus.Done, TaskPriority.Low);
        await SeedTaskAsync(seed.ProjectId, "Delta", TaskItemStatus.Done, TaskPriority.Medium);

        var filtered = await ReadAsync<PagedResponse<TaskResponse>>(
            await Client.GetAsync("/api/tasks?status=Done&sort=-title"));
        Assert.Equal(3, filtered.TotalCount);
        Assert.Equal(new[] { "Delta", "Charlie", "Alpha" }, filtered.Items.Select(t => t.Title).ToArray());

        var secondPage = await ReadAsync<PagedResponse<TaskResponse>>(
            await Client.GetAsync($"/api/projects/{seed.ProjectId}/tasks?pageSize=3&page=2&sort=title"));
        Assert.Equal(4, secondPage.TotalCount);
        Assert.Equal(2, secondPage.TotalPages);
        Assert.Equal("Delta", Assert.Single(secondPage.Items).Title);

        var byPriority = await ReadAsync<PagedResponse<TaskResponse>>(
            await Client.GetAsync("/api/tasks?sort=-priority,title"));
        Assert.Equal("Alpha", byPriority.Items[0].Title);

        var searched = await ReadAsync<PagedResponse<TaskResponse>>(await Client.GetAsync("/api/tasks?search=RAV"));
        Assert.Equal("Bravo", Assert.Single(searched.Items).Title);
    }

    [Fact]
    public async Task List_with_unknown_sort_field_returns_400()
    {
        var response = await Client.GetAsync("/api/tasks?sort=password");

        AssertProblem(response, HttpStatusCode.BadRequest);
    }
}
