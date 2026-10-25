using System.Net;
using System.Text.Json;
using TaskTracker.Tests.Infrastructure;

namespace TaskTracker.Tests.Api;

/// <summary>The OpenAPI document and Swagger UI are served, and the key endpoints are described in them.</summary>
[Collection(ApiCollection.Name)]
public sealed class OpenApiTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private static readonly (string Path, string Method)[] KeyOperations =
    [
        ("/api/projects", "get"),
        ("/api/projects", "post"),
        ("/api/projects/{id}", "get"),
        ("/api/projects/{id}", "put"),
        ("/api/projects/{id}", "delete"),
        ("/api/projects/{id}/members/{userId}", "put"),
        ("/api/projects/{id}/members/{userId}", "delete"),
        ("/api/tasks", "get"),
        ("/api/projects/{projectId}/tasks", "get"),
        ("/api/projects/{projectId}/tasks", "post"),
        ("/api/tasks/{id}", "get"),
        ("/api/tasks/{id}", "put"),
        ("/api/tasks/{id}", "delete"),
        ("/api/tasks/{id}/status", "patch"),
        ("/api/tasks/{taskId}/comments", "get"),
        ("/api/tasks/{taskId}/comments", "post"),
        ("/api/comments/{id}", "get"),
        ("/api/users", "get"),
        ("/api/users", "post"),
        ("/api/users/{id}", "get"),
        ("/api/tasks/{taskId}/attachments", "get"),
        ("/api/tasks/{taskId}/attachments", "post"),
        ("/api/attachments/{id}", "get"),
        ("/api/attachments/{id}/download", "get"),
        ("/api/attachments/{id}", "delete")
    ];

    private async Task<JsonDocument> GetSpecAsync()
    {
        var response = await Client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return await ReadJsonAsync(response);
    }

    [Fact]
    public async Task Specification_is_valid_openapi_3_with_metadata()
    {
        using var spec = await GetSpecAsync();

        var root = spec.RootElement;
        Assert.StartsWith("3.", root.GetProperty("openapi").GetString());
        Assert.Equal("TaskTracker API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("info").GetProperty("description").GetString()));
    }

    [Fact]
    public async Task Every_key_endpoint_is_present_and_has_summary_and_responses()
    {
        using var spec = await GetSpecAsync();
        var paths = spec.RootElement.GetProperty("paths");

        foreach (var (path, method) in KeyOperations)
        {
            Assert.True(paths.TryGetProperty(path, out var pathItem), $"missing path {path}");
            Assert.True(pathItem.TryGetProperty(method, out var operation), $"missing {method.ToUpperInvariant()} {path}");
            Assert.True(
                operation.TryGetProperty("summary", out var summary) && !string.IsNullOrWhiteSpace(summary.GetString()),
                $"{method.ToUpperInvariant()} {path} has no summary (XML comment missing?)");
            Assert.True(operation.TryGetProperty("responses", out var responses) && responses.EnumerateObject().Any(),
                $"{method.ToUpperInvariant()} {path} has no responses");
        }
    }

    [Fact]
    public async Task Errors_are_documented_as_problem_json()
    {
        using var spec = await GetSpecAsync();

        var notFound = spec.RootElement.GetProperty("paths").GetProperty("/api/tasks/{id}").GetProperty("get")
            .GetProperty("responses").GetProperty("404").GetProperty("content");

        Assert.True(notFound.TryGetProperty("application/problem+json", out _));
    }

    [Fact]
    public async Task Upload_endpoint_is_described_as_multipart_with_413_and_415()
    {
        using var spec = await GetSpecAsync();

        var upload = spec.RootElement.GetProperty("paths").GetProperty("/api/tasks/{taskId}/attachments").GetProperty("post");

        var content = upload.GetProperty("requestBody").GetProperty("content");
        Assert.True(content.TryGetProperty("multipart/form-data", out var multipart));
        Assert.True(multipart.GetProperty("schema").GetProperty("properties").TryGetProperty("file", out var file));
        Assert.Equal("binary", file.GetProperty("format").GetString());

        var responses = upload.GetProperty("responses");
        foreach (var code in new[] { "201", "400", "404", "413", "415" })
        {
            Assert.True(responses.TryGetProperty(code, out _), $"upload must document {code}");
        }
    }

    [Fact]
    public async Task Enums_are_documented_as_strings()
    {
        using var spec = await GetSpecAsync();

        var status = spec.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("TaskItemStatus");

        Assert.Equal("string", status.GetProperty("type").GetString());
        Assert.Contains("InProgress", status.GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Swagger_ui_page_is_available()
    {
        var response = await Client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("swagger-ui", html);
    }
}
