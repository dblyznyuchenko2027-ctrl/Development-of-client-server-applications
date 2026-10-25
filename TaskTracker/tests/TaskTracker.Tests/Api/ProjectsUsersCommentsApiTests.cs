using System.Net;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Domain;
using TaskTracker.Api.Dtos;
using TaskTracker.Tests.Infrastructure;

namespace TaskTracker.Tests.Api;

/// <summary>Projects, users and comments: key scenarios incl. database state.</summary>
[Collection(ApiCollection.Name)]
public sealed class ProjectsUsersCommentsApiTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Create_project_returns_201_and_adds_owner_as_member()
    {
        var seed = await SeedProjectAsync();

        var response = await PostJsonAsync("/api/projects", new { name = "Second", description = "d", ownerId = seed.MemberId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync<ProjectDetailResponse>(response);
        Assert.Equal($"/api/projects/{body.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(seed.MemberId, body.Owner.Id);

        var members = await WithDbAsync(db => db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == body.Id).ToListAsync());
        Assert.Equal(seed.MemberId, Assert.Single(members).UserId);
    }

    [Fact]
    public async Task Create_project_with_unknown_owner_returns_400()
    {
        var response = await PostJsonAsync("/api/projects", new { name = "No owner", ownerId = 999 });

        AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal(0, await WithDbAsync(db => db.Projects.CountAsync()));
    }

    [Fact]
    public async Task Delete_project_cascades_to_tasks_and_memberships()
    {
        var seed = await SeedProjectAsync();
        await SeedTaskAsync(seed.ProjectId);
        await SeedTaskAsync(seed.ProjectId);

        var response = await Client.DeleteAsync($"/api/projects/{seed.ProjectId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.Projects.CountAsync()));
        Assert.Equal(0, await WithDbAsync(db => db.Tasks.CountAsync()));
        Assert.Equal(0, await WithDbAsync(db => db.ProjectMembers.CountAsync()));
        Assert.Equal(3, await WithDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task Add_member_is_idempotent_and_remove_owner_is_conflict()
    {
        var seed = await SeedProjectAsync();

        var first = await Client.PutAsync($"/api/projects/{seed.ProjectId}/members/{seed.OutsiderId}", null);
        var second = await Client.PutAsync($"/api/projects/{seed.ProjectId}/members/{seed.OutsiderId}", null);
        var removeOwner = await Client.DeleteAsync($"/api/projects/{seed.ProjectId}/members/{seed.ManagerId}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        AssertProblem(removeOwner, HttpStatusCode.Conflict);
        Assert.Equal(3, await WithDbAsync(db => db.ProjectMembers.CountAsync(m => m.ProjectId == seed.ProjectId)));
    }

    [Fact]
    public async Task Create_user_with_duplicate_email_returns_409_and_keeps_one_row()
    {
        var first = await PostJsonAsync("/api/users", new { email = "dup@test.local", displayName = "First" });
        var second = await PostJsonAsync("/api/users", new { email = "dup@test.local", displayName = "Second" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        AssertProblem(second, HttpStatusCode.Conflict);
        var users = await WithDbAsync(db => db.Users.AsNoTracking().Where(u => u.Email == "dup@test.local").ToListAsync());
        var user = Assert.Single(users);
        Assert.Equal("First", user.DisplayName);
        Assert.Equal(UserRole.Member, user.Role);
    }

    [Fact]
    public async Task Create_user_with_invalid_email_returns_400()
    {
        var response = await PostJsonAsync("/api/users", new { email = "not-an-email", displayName = "X" });

        AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal(0, await WithDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task Comment_lifecycle_create_update_delete()
    {
        var seed = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(seed.ProjectId);

        var created = await PostJsonAsync($"/api/tasks/{taskId}/comments", new { text = "Looks good", authorId = seed.MemberId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var comment = await ReadAsync<CommentResponse>(created);
        var saved = await WithDbAsync(db => db.Comments.AsNoTracking().SingleAsync(c => c.Id == comment.Id));
        Assert.Equal(taskId, saved.TaskItemId);
        Assert.Equal(seed.MemberId, saved.AuthorId);

        var updated = await PutJsonAsync($"/api/comments/{comment.Id}", new { text = "Looks great" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Looks great", (await WithDbAsync(db => db.Comments.AsNoTracking().SingleAsync())).Text);

        var deleted = await Client.DeleteAsync($"/api/comments/{comment.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.Comments.CountAsync()));
    }

    [Fact]
    public async Task Create_comment_with_unknown_author_returns_400()
    {
        var seed = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(seed.ProjectId);

        var response = await PostJsonAsync($"/api/tasks/{taskId}/comments", new { text = "ghost", authorId = 4040 });

        AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal(0, await WithDbAsync(db => db.Comments.CountAsync()));
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy_database()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
