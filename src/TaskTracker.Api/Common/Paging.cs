namespace TaskTracker.Api.Common;

public sealed record PageRequest(int Page, int PageSize)
{
    public int Offset => (Page - 1) * PageSize;
}

/// <summary>Result of a repository page query: one page of items plus the total number of matches.</summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int TotalCount)
{
    public PagedResponse<TOut> ToResponse<TOut>(PageRequest page, Func<T, TOut> map)
    {
        var totalPages = (int)Math.Ceiling(TotalCount / (double)page.PageSize);
        return new PagedResponse<TOut>(Items.Select(map).ToList(), page.Page, page.PageSize, TotalCount, totalPages);
    }
}

/// <summary>Envelope returned by every collection endpoint.</summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
