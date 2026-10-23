namespace TaskTracker.Api.Common;

public sealed record SortField(string Name, bool Descending);

/// <summary>Parses the <c>sort</c> query parameter: <c>sort=-createdAt,title</c> (prefix '-' means descending).</summary>
public static class SortParser
{
    public const int MaxFields = 3;

    public static IReadOnlyList<SortField> Parse(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return Array.Empty<SortField>();
        }

        var fields = new List<SortField>();
        foreach (var raw in sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var descending = raw.StartsWith('-');
            var name = descending ? raw[1..] : raw;
            if (name.Length == 0)
            {
                throw new ValidationFailedException("sort", "Sort field name cannot be empty.");
            }

            fields.Add(new SortField(name, descending));
        }

        if (fields.Count > MaxFields)
        {
            throw new ValidationFailedException("sort", $"At most {MaxFields} sort fields are allowed.");
        }

        return fields;
    }
}
