using System.ComponentModel.DataAnnotations;

namespace TaskTracker.Api.Dtos;

/// <summary>AuthorId is taken from the body until authentication is introduced.</summary>
public record CreateCommentRequest(
    [property: Required, StringLength(2000, MinimumLength = 1)] string Text,
    [property: Range(1, int.MaxValue)] int AuthorId);

public record UpdateCommentRequest(
    [property: Required, StringLength(2000, MinimumLength = 1)] string Text);

public record CommentResponse(int Id, string Text, int TaskId, UserSummary Author, DateTime CreatedAt);

public class CommentListQuery : PageQuery
{
}
