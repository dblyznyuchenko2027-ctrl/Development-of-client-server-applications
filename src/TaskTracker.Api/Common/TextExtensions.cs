namespace TaskTracker.Api.Common;

public static class TextExtensions
{
    /// <summary>Trims the value and turns blank strings into null (for optional text columns).</summary>
    public static string? NullIfBlank(this string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
