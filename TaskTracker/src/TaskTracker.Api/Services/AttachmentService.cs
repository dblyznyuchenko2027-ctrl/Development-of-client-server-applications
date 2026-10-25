using Microsoft.Extensions.Options;
using TaskTracker.Api.Common;
using TaskTracker.Api.Domain;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Infrastructure.Storage;
using TaskTracker.Api.Repositories;

namespace TaskTracker.Api.Services;

public class AttachmentService(
    IAttachmentRepository attachments,
    ITaskRepository tasks,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    IOptions<FileStorageOptions> options) : IAttachmentService
{
    private const int MaxFileNameLength = 255;

    private readonly FileStorageOptions _options = options.Value;

    public async Task<IReadOnlyList<AttachmentResponse>> ListAsync(int taskId, CancellationToken ct = default)
    {
        await EnsureTaskExistsAsync(taskId, ct);
        var items = await attachments.ListByTaskAsync(taskId, ct);
        return items.Select(a => a.ToResponse()).ToList();
    }

    public async Task<AttachmentResponse> UploadAsync(int taskId, IFormFile? file, CancellationToken ct = default)
    {
        await EnsureTaskExistsAsync(taskId, ct);

        if (file is null || file.Length == 0)
        {
            throw new ValidationFailedException("file", "A non-empty file must be sent in the 'file' form field.");
        }

        if (file.Length > _options.MaxFileSizeBytes)
        {
            throw new PayloadTooLargeException(
                $"The file is {file.Length} bytes; the maximum allowed size is {_options.MaxFileSizeBytes} bytes.");
        }

        var fileName = SanitizeFileName(file.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = AllowedFileTypes.ContentTypeFor(extension);
        if (contentType is null || !AllowedFileTypes.IsClientContentTypeAcceptable(extension, file.ContentType))
        {
            throw new UnsupportedFileTypeException(
                $"Files of this type are not allowed. Allowed extensions: {string.Join(", ", AllowedFileTypes.Extensions)}.");
        }

        await EnsureContentMatchesAsync(file, extension, ct);

        if (await attachments.CountByTaskAsync(taskId, ct) >= _options.MaxFilesPerTask)
        {
            throw new BusinessRuleException($"A task cannot have more than {_options.MaxFilesPerTask} attachments.");
        }

        string storagePath;
        await using (var stream = file.OpenReadStream())
        {
            storagePath = await storage.SaveAsync(taskId.ToString(), extension, stream, ct);
        }

        var attachment = new Attachment
        {
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = file.Length,
            StoragePath = storagePath,
            TaskItemId = taskId
        };

        try
        {
            await attachments.AddAsync(attachment, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            // Keep disk and database consistent: no row -> no file.
            storage.Delete(storagePath);
            throw;
        }

        return attachment.ToResponse();
    }

    public async Task<AttachmentResponse> GetAsync(int id, CancellationToken ct = default)
    {
        var attachment = await attachments.GetByIdAsync(id, ct) ?? throw new NotFoundException("Attachment", id);
        return attachment.ToResponse();
    }

    public async Task<AttachmentFile> OpenAsync(int id, CancellationToken ct = default)
    {
        var attachment = await attachments.GetByIdAsync(id, ct) ?? throw new NotFoundException("Attachment", id);
        var stream = storage.OpenRead(attachment.StoragePath)
            ?? throw new NotFoundException($"The stored file of attachment {id} is missing.");

        return new AttachmentFile(stream, attachment.ContentType, attachment.FileName);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var attachment = await attachments.GetByIdAsync(id, ct) ?? throw new NotFoundException("Attachment", id);

        attachments.Remove(attachment);
        await unitOfWork.SaveChangesAsync(ct);

        // After the commit: if the DB delete fails the file must stay available.
        storage.Delete(attachment.StoragePath);
    }

    private async Task EnsureTaskExistsAsync(int taskId, CancellationToken ct)
    {
        if (!await tasks.ExistsAsync(taskId, ct))
        {
            throw new NotFoundException("Task", taskId);
        }
    }

    private static async Task EnsureContentMatchesAsync(IFormFile file, string extension, CancellationToken ct)
    {
        var header = new byte[(int)Math.Min(file.Length, AllowedFileTypes.HeaderLength)];
        await using var stream = file.OpenReadStream();
        await stream.ReadExactlyAsync(header, ct);

        if (!AllowedFileTypes.ContentMatches(extension, header))
        {
            throw new UnsupportedFileTypeException($"The file content does not match its '{extension}' extension.");
        }
    }

    /// <summary>Drops any client-supplied path, control characters and over-long names (keeping the extension).</summary>
    private static string SanitizeFileName(string? rawName)
    {
        var name = rawName ?? string.Empty;
        var lastSeparator = name.LastIndexOfAny(['/', '\\']);
        if (lastSeparator >= 0)
        {
            name = name[(lastSeparator + 1)..];
        }

        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0 || name is "." or "..")
        {
            throw new ValidationFailedException("file", "The file must have a name.");
        }

        if (name.Length > MaxFileNameLength)
        {
            var extension = Path.GetExtension(name);
            if (extension.Length > 20)
            {
                extension = string.Empty;
            }

            name = name[..(MaxFileNameLength - extension.Length)] + extension;
        }

        return name;
    }
}
