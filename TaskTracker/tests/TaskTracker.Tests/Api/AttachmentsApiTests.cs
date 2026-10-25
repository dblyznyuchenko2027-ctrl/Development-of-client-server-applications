using System.Net;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Dtos;
using TaskTracker.Tests.Infrastructure;

namespace TaskTracker.Tests.Api;

/// <summary>File upload: type and size validation, storing on disk + metadata in the database, returning the file.</summary>
[Collection(ApiCollection.Name)]
public sealed class AttachmentsApiTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const int MaxFileSize = 5 * 1024 * 1024;

    private async Task<int> SeedTaskIdAsync()
    {
        var seed = await SeedProjectAsync();
        return await SeedTaskAsync(seed.ProjectId);
    }

    [Fact]
    public async Task Upload_png_returns_201_stores_file_and_metadata()
    {
        var taskId = await SeedTaskIdAsync();

        var response = await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Png, "pixel.png", "image/png"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync<AttachmentResponse>(response);
        Assert.Equal($"/api/attachments/{body.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(taskId, body.TaskId);
        Assert.Equal("pixel.png", body.FileName);
        Assert.Equal("image/png", body.ContentType);
        Assert.Equal(SampleFiles.Png.Length, body.SizeBytes);

        // database state
        var row = await WithDbAsync(db => db.Attachments.AsNoTracking().SingleAsync(a => a.Id == body.Id));
        Assert.Equal(taskId, row.TaskItemId);
        Assert.Equal("pixel.png", row.FileName);
        Assert.Equal(SampleFiles.Png.Length, row.SizeBytes);

        // file system state: exactly one file, stored under a generated name (not the client's one)
        var stored = Assert.Single(StoredFiles());
        Assert.Equal(SampleFiles.Png, await File.ReadAllBytesAsync(stored));
        Assert.NotEqual("pixel.png", Path.GetFileName(stored));
        Assert.Equal(".png", Path.GetExtension(stored));
    }

    [Fact]
    public async Task Uploaded_file_is_returned_byte_for_byte_with_correct_headers()
    {
        var taskId = await SeedTaskIdAsync();
        var upload = await ReadAsync<AttachmentResponse>(
            await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Pdf, "spec sheet.pdf", "application/pdf")));

        var response = await Client.GetAsync($"/api/attachments/{upload.Id}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("spec sheet.pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(SampleFiles.Pdf, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Task_details_and_attachment_list_include_uploaded_files()
    {
        var taskId = await SeedTaskIdAsync();
        await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Text, "notes.txt", "text/plain"));
        await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Png, "pixel.png", "image/png"));

        var details = await ReadAsync<TaskDetailResponse>(await Client.GetAsync($"/api/tasks/{taskId}"));
        var list = await ReadAsync<List<AttachmentResponse>>(await Client.GetAsync($"/api/tasks/{taskId}/attachments"));

        Assert.Equal(new[] { "notes.txt", "pixel.png" }, details.Attachments.Select(a => a.FileName).ToArray());
        Assert.Equal(new[] { "notes.txt", "pixel.png" }, list.Select(a => a.FileName).ToArray());
        Assert.Equal(2, StoredFiles().Length);
    }

    [Fact]
    public async Task Upload_with_unsupported_extension_returns_415_and_stores_nothing()
    {
        var taskId = await SeedTaskIdAsync();

        var response = await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Executable, "setup.exe", "application/octet-stream"));

        AssertProblem(response, HttpStatusCode.UnsupportedMediaType);
        Assert.Equal(0, await WithDbAsync(db => db.Attachments.CountAsync()));
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Upload_executable_renamed_to_png_is_rejected_by_content_check()
    {
        var taskId = await SeedTaskIdAsync();

        var response = await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Executable, "innocent.png", "image/png"));

        AssertProblem(response, HttpStatusCode.UnsupportedMediaType);
        Assert.Equal(0, await WithDbAsync(db => db.Attachments.CountAsync()));
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Upload_with_mime_type_that_does_not_match_extension_returns_415()
    {
        var taskId = await SeedTaskIdAsync();

        var response = await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Png, "pixel.png", "text/html"));

        AssertProblem(response, HttpStatusCode.UnsupportedMediaType);
        Assert.Equal(0, await WithDbAsync(db => db.Attachments.CountAsync()));
    }

    [Fact]
    public async Task Upload_binary_content_as_txt_is_rejected()
    {
        var taskId = await SeedTaskIdAsync();

        var response = await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Png, "notes.txt", "text/plain"));

        AssertProblem(response, HttpStatusCode.UnsupportedMediaType);
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Upload_file_over_size_limit_returns_413_and_stores_nothing()
    {
        var taskId = await SeedTaskIdAsync();

        var response = await Client.PostAsync(
            $"/api/tasks/{taskId}/attachments",
            FileForm(SampleFiles.PngOfSize(MaxFileSize + 1), "huge.png", "image/png"));

        AssertProblem(response, HttpStatusCode.RequestEntityTooLarge);
        Assert.Equal(0, await WithDbAsync(db => db.Attachments.CountAsync()));
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Upload_file_exactly_at_size_limit_is_accepted()
    {
        var taskId = await SeedTaskIdAsync();

        var response = await Client.PostAsync(
            $"/api/tasks/{taskId}/attachments",
            FileForm(SampleFiles.PngOfSize(MaxFileSize), "limit.png", "image/png"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(MaxFileSize, (await ReadAsync<AttachmentResponse>(response)).SizeBytes);
    }

    [Fact]
    public async Task Upload_empty_file_returns_400()
    {
        var taskId = await SeedTaskIdAsync();

        var response = await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm([], "empty.txt", "text/plain"));

        AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal(0, await WithDbAsync(db => db.Attachments.CountAsync()));
    }

    [Fact]
    public async Task Upload_without_file_field_returns_400()
    {
        var taskId = await SeedTaskIdAsync();

        var response = await Client.PostAsync(
            $"/api/tasks/{taskId}/attachments",
            FileForm(SampleFiles.Png, "pixel.png", "image/png", field: "document"));

        AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal(0, await WithDbAsync(db => db.Attachments.CountAsync()));
    }

    [Fact]
    public async Task Upload_to_unknown_task_returns_404_and_stores_nothing()
    {
        var response = await Client.PostAsync("/api/tasks/777/attachments", FileForm(SampleFiles.Png, "pixel.png", "image/png"));

        AssertProblem(response, HttpStatusCode.NotFound);
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Client_supplied_path_in_file_name_is_ignored()
    {
        var taskId = await SeedTaskIdAsync();

        var response = await Client.PostAsync(
            $"/api/tasks/{taskId}/attachments",
            FileForm(SampleFiles.Png, "../../outside/evil.png", "image/png"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("evil.png", (await ReadAsync<AttachmentResponse>(response)).FileName);
        var stored = Assert.Single(StoredFiles());
        Assert.StartsWith(Path.GetFullPath(Fixture.StoragePath), Path.GetFullPath(stored));
    }

    [Fact]
    public async Task Delete_attachment_removes_row_and_file()
    {
        var taskId = await SeedTaskIdAsync();
        var upload = await ReadAsync<AttachmentResponse>(
            await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Text, "notes.txt", "text/plain")));
        Assert.Single(StoredFiles());

        var response = await Client.DeleteAsync($"/api/attachments/{upload.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.Attachments.CountAsync()));
        Assert.Empty(StoredFiles());
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/attachments/{upload.Id}/download")).StatusCode);
    }

    [Fact]
    public async Task Deleting_task_removes_its_files_from_disk()
    {
        var taskId = await SeedTaskIdAsync();
        await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Text, "a.txt", "text/plain"));
        await Client.PostAsync($"/api/tasks/{taskId}/attachments", FileForm(SampleFiles.Png, "b.png", "image/png"));
        Assert.Equal(2, StoredFiles().Length);

        var response = await Client.DeleteAsync($"/api/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.Attachments.CountAsync()));
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Deleting_project_removes_files_of_all_its_tasks()
    {
        var seed = await SeedProjectAsync();
        var first = await SeedTaskAsync(seed.ProjectId, "one");
        var second = await SeedTaskAsync(seed.ProjectId, "two");
        await Client.PostAsync($"/api/tasks/{first}/attachments", FileForm(SampleFiles.Text, "a.txt", "text/plain"));
        await Client.PostAsync($"/api/tasks/{second}/attachments", FileForm(SampleFiles.Text, "b.txt", "text/plain"));
        Assert.Equal(2, StoredFiles().Length);

        var response = await Client.DeleteAsync($"/api/projects/{seed.ProjectId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.Attachments.CountAsync()));
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Download_unknown_attachment_returns_404_problem()
    {
        var response = await Client.GetAsync("/api/attachments/4242/download");

        AssertProblem(response, HttpStatusCode.NotFound);
    }
}
