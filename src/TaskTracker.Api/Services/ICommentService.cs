using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;

namespace TaskTracker.Api.Services;

public interface ICommentService
{
    Task<PagedResponse<CommentResponse>> ListAsync(int taskId, CommentListQuery query, CancellationToken ct = default);

    Task<CommentResponse> GetAsync(int id, CancellationToken ct = default);

    Task<CommentResponse> CreateAsync(int taskId, CreateCommentRequest request, CancellationToken ct = default);

    Task<CommentResponse> UpdateAsync(int id, UpdateCommentRequest request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
