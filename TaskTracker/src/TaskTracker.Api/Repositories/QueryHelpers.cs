using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Common;

namespace TaskTracker.Api.Repositories;

/// <summary>
/// Whitelist of fields a collection can be sorted by. Unknown names are rejected with a 400 instead of being
/// passed to the database; the id tiebreaker keeps pagination stable between requests.
/// </summary>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, Func<IQueryable<T>, bool, IOrderedQueryable<T>>> first =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Func<IOrderedQueryable<T>, bool, IOrderedQueryable<T>>> next =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly SortField defaultSort;
    private readonly Expression<Func<T, int>> idKey;

    public SortMap(SortField defaultSort, Expression<Func<T, int>> idKey)
    {
        this.defaultSort = defaultSort;
        this.idKey = idKey;
    }

    public SortMap<T> Add<TKey>(string name, Expression<Func<T, TKey>> key)
    {
        first[name] = (q, descending) => descending ? q.OrderByDescending(key) : q.OrderBy(key);
        next[name] = (q, descending) => descending ? q.ThenByDescending(key) : q.ThenBy(key);
        return this;
    }

    public IQueryable<T> Apply(IQueryable<T> query, IReadOnlyList<SortField> fields)
    {
        IReadOnlyList<SortField> effective = fields.Count == 0 ? new[] { defaultSort } : fields;

        IOrderedQueryable<T>? ordered = null;
        foreach (var field in effective)
        {
            if (!first.ContainsKey(field.Name))
            {
                throw new ValidationFailedException(
                    "sort",
                    $"Unknown sort field '{field.Name}'. Allowed fields: {string.Join(", ", first.Keys.Order())}.");
            }

            ordered = ordered is null
                ? first[field.Name](query, field.Descending)
                : next[field.Name](ordered, field.Descending);
        }

        return ordered!.ThenBy(idKey);
    }
}

public static class SqlLike
{
    /// <summary>Builds an ILIKE pattern for "contains", escaping the wildcard characters typed by the user.</summary>
    public static string Contains(string term) =>
        "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}

public static class QueryableExtensions
{
    /// <summary>
    /// Counts the filtered query, then loads one sorted page. <paramref name="include"/> (eager loading) is applied
    /// only to the page query. Two SQL commands in total, regardless of the page size.
    /// </summary>
    public static async Task<PagedList<T>> ToPagedListAsync<T>(
        this IQueryable<T> filtered,
        SortMap<T> sortMap,
        IReadOnlyList<SortField> sort,
        PageRequest page,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken ct = default)
    {
        var total = await filtered.CountAsync(ct);
        var shaped = include is null ? filtered : include(filtered);
        var items = await sortMap.Apply(shaped, sort).Skip(page.Offset).Take(page.PageSize).ToListAsync(ct);
        return new PagedList<T>(items, total);
    }
}
