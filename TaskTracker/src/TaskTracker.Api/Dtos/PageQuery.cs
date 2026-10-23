using System.ComponentModel.DataAnnotations;
using TaskTracker.Api.Common;

namespace TaskTracker.Api.Dtos;

/// <summary>Common query-string parameters of every collection endpoint: page, pageSize, sort.</summary>
public class PageQuery
{
    public const int MaxPageSize = 100;

    [Range(1, 100_000)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = 20;

    /// <summary>Comma-separated fields, '-' prefix = descending. Example: <c>-createdAt,title</c>.</summary>
    [StringLength(200)]
    public string? Sort { get; init; }

    public PageRequest ToPageRequest() => new(Page, PageSize);

    public IReadOnlyList<SortField> ParseSort() => SortParser.Parse(Sort);
}
