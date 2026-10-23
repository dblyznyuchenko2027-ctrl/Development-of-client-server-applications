using TaskTracker.Api.Common;
using TaskTracker.Api.Domain;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Repositories;

namespace TaskTracker.Api.Services;

public class CommentService(
    ICommentRepository comments,
    ITaskRepository tasks,
    IUserRepository users,
    IUnitOfWork unitOfWork) : ICommentService
{
    public async Task<PagedResponse<CommentResponse>> ListAsync(int taskId, CommentListQuery query, CancellationToken ct = default)
    {
        if (!await tasks.ExistsAsync(taskId, ct))
        {
            throw new NotFoundException("Task", taskId);
        }

        var page = query.ToPageRequest();
        var result = await comments.SearchByTaskAsync(taskId, query.ParseSort(), page, ct);
        return result.ToResponse(page, c => c.ToResponse());
    }

    public async Task<CommentResponse> GetAsync(int id, CancellationToken ct = default)
    {
        var comment = await comments.GetWithAuthorAsync(id, ct) ?? throw new NotFoundException("Comment", id);
        return comment.ToResponse();
    }

    public async Task<CommentResponse> CreateAsync(int taskId, CreateCommentRequest request, CancellationToken ct = default)
    {
        if (!await tasks.ExistsAsync(taskId, ct))
        {
            throw new NotFoundException("Task", taskId);
        }

        if (!await users.ExistsAsync(request.AuthorId, ct))
        {
            throw new ValidationFailedException("authorId", $"User with id {request.AuthorId} does not exist.");
        }

        var comment = new Comment
        {
            Text = request.Text.Trim(),
            TaskItemId = taskId,
            AuthorId = request.AuthorId
        };

        await comments.AddAsync(comment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(comment.Id, ct);
    }

    public async Task<CommentResponse> UpdateAsync(int id, UpdateCommentRequest request, CancellationToken ct = default)
    {
        var comment = await comments.GetByIdAsync(id, ct) ?? throw new NotFoundException("Comment", id);

        comment.Text = request.Text.Trim();
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var comment = await comments.GetByIdAsync(id, ct) ?? throw new NotFoundException("Comment", id);

        comments.Remove(comment);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
