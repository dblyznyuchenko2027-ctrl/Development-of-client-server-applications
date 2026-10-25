using TaskTracker.Api.Dtos;

namespace TaskTracker.Api.Services;

/// <summary>A stored attachment ready to be streamed to the client. The caller owns (and disposes) <see cref="Content"/>.</summary>
public sealed record AttachmentFile(Stream Content, string ContentType, string FileName);

public interface IAttachmentService
{
    Task<IReadOnlyList<AttachmentResponse>> ListAsync(int taskId, CancellationToken ct = default);

    Task<AttachmentResponse> UploadAsync(int taskId, IFormFile? file, CancellationToken ct = default);

    Task<AttachmentResponse> GetAsync(int id, CancellationToken ct = default);

    Task<AttachmentFile> OpenAsync(int id, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
