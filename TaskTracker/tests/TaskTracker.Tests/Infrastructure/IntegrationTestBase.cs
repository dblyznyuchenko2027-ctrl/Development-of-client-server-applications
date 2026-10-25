using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using TaskTracker.Api.Data;
using TaskTracker.Api.Domain;

namespace TaskTracker.Tests.Infrastructure;

/// <summary>Ids of the entities created by <see cref="IntegrationTestBase.SeedProjectAsync"/>.</summary>
public sealed record SeededProject(int ManagerId, int MemberId, int OutsiderId, int ProjectId);

/// <summary>
/// Base class of integration tests: every test starts with empty tables, talks to the app only through HTTP and
/// verifies the database state through a separate <see cref="AppDbContext"/>.
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase(ApiFixture fixture) : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected ApiFixture Fixture { get; } = fixture;

    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Fixture.ResetAsync();
        Client = Fixture.CreateClient();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    // ---------- database access (arrange / assert) ----------

    protected async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Fixture.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>manager (project owner + member), member (project member), outsider (not in the project), one project.</summary>
    protected Task<SeededProject> SeedProjectAsync() =>
        WithDbAsync(async db =>
        {
            var manager = new User { Email = "manager@test.local", DisplayName = "Manager", Role = UserRole.ProjectManager };
            var member = new User { Email = "member@test.local", DisplayName = "Member" };
            var outsider = new User { Email = "outsider@test.local", DisplayName = "Outsider" };
            db.Users.AddRange(manager, member, outsider);
            await db.SaveChangesAsync();

            var project = new Project
            {
                Name = "Test project",
                OwnerId = manager.Id,
                Members =
                {
                    new ProjectMember { UserId = manager.Id },
                    new ProjectMember { UserId = member.Id }
                }
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            return new SeededProject(manager.Id, member.Id, outsider.Id, project.Id);
        });

    protected Task<int> SeedTaskAsync(
        int projectId,
        string title = "Seeded task",
        TaskItemStatus status = TaskItemStatus.Todo,
        TaskPriority priority = TaskPriority.Medium,
        int? assigneeId = null) =>
        WithDbAsync(async db =>
        {
            var task = new TaskItem { Title = title, Status = status, Priority = priority, ProjectId = projectId, AssigneeId = assigneeId };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();
            return task.Id;
        });

    // ---------- HTTP helpers ----------

    protected Task<HttpResponseMessage> PostJsonAsync(string url, object body) => Client.PostAsJsonAsync(url, body, Json);

    protected Task<HttpResponseMessage> PutJsonAsync(string url, object body) => Client.PutAsJsonAsync(url, body, Json);

    protected Task<HttpResponseMessage> PatchJsonAsync(string url, object body) =>
        Client.PatchAsync(url, JsonContent.Create(body, options: Json));

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    protected static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    /// <summary>Builds a <c>multipart/form-data</c> body with a single file field.</summary>
    protected static MultipartFormDataContent FileForm(byte[] bytes, string fileName, string? contentType, string field = "file")
    {
        var file = new ByteArrayContent(bytes);
        if (contentType is not null)
        {
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        return new MultipartFormDataContent { { file, field, fileName } };
    }

    protected static void AssertProblem(HttpResponseMessage response, System.Net.HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    protected string[] StoredFiles() =>
        Directory.Exists(Fixture.StoragePath)
            ? Directory.GetFiles(Fixture.StoragePath, "*", SearchOption.AllDirectories)
            : [];
}
