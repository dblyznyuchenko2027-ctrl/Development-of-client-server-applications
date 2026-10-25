using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Infrastructure.OpenApi;
using TaskTracker.Api.Services;

namespace TaskTracker.Api.Controllers;

/// <summary>Files attached to tasks: upload (type and size validated), list, download and delete.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
[Tags("Attachments")]
public class AttachmentsController(IAttachmentService service) : ControllerBase
{
    /// <summary>Hard cap of the whole request body; the per-file limit from configuration (default 5 MiB) is stricter.</summary>
    private const long MaxRequestBytes = 10 * 1024 * 1024;

    /// <summary>List the attachments of a task.</summary>
    /// <param name="taskId">Task id.</param>
    [HttpGet("tasks/{taskId:int}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<AttachmentResponse>), StatusCodes.Status200OK)]
    [ProducesNotFound]
    public async Task<ActionResult<IReadOnlyList<AttachmentResponse>>> ListByTask(int taskId, CancellationToken ct) =>
        Ok(await service.ListAsync(taskId, ct));

    /// <summary>Upload a file and attach it to a task.</summary>
    /// <remarks>
    /// Send <c>multipart/form-data</c> with one file in the <c>file</c> field.
    /// Allowed: <c>.png .jpg .jpeg .gif .pdf .txt .md .csv</c>, up to 5 MiB (configurable, <c>Storage:MaxFileSizeBytes</c>).
    /// The content type is checked against the extension and the first bytes of the file, so a renamed file is rejected.
    /// </remarks>
    /// <param name="taskId">Task id.</param>
    /// <param name="file">The file to upload.</param>
    /// <response code="201">The file was stored; <c>Location</c> points to its metadata.</response>
    [HttpPost("tasks/{taskId:int}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(AttachmentResponse), StatusCodes.Status201Created)]
    [ProducesBadRequest]
    [ProducesNotFound]
    [ProducesPayloadTooLarge]
    [ProducesUnsupportedFileType]
    [ProducesUnprocessable]
    public async Task<ActionResult<AttachmentResponse>> Upload(int taskId, IFormFile? file, CancellationToken ct)
    {
        var created = await service.UploadAsync(taskId, file, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Get attachment metadata (name, type, size).</summary>
    /// <param name="id">Attachment id.</param>
    [HttpGet("attachments/{id:int}")]
    [ProducesResponseType(typeof(AttachmentResponse), StatusCodes.Status200OK)]
    [ProducesNotFound]
    public async Task<ActionResult<AttachmentResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>Download the file content.</summary>
    /// <remarks>Returned with the original file name in <c>Content-Disposition</c> and the validated MIME type. Range requests are supported.</remarks>
    /// <param name="id">Attachment id.</param>
    [HttpGet("attachments/{id:int}/download")]
    [Produces("application/octet-stream", "image/png", "image/jpeg", "image/gif", "application/pdf", "text/plain", "text/markdown", "text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesNotFound]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var file = await service.OpenAsync(id, ct);

        // Never let the browser guess a different type for user-supplied content.
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true);
    }

    /// <summary>Delete an attachment (database row and stored file).</summary>
    /// <param name="id">Attachment id.</param>
    [HttpDelete("attachments/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesNotFound]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
