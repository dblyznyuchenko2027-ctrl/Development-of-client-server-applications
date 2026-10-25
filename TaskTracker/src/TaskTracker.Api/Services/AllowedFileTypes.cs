namespace TaskTracker.Api.Services;

/// <summary>
/// Whitelist of attachment types. A file is accepted only when its extension is known, the MIME type sent by the
/// client (if any) is compatible with it and the first bytes of the content match the type
/// (magic number for binary formats, "no NUL bytes" for text formats), so renaming <c>virus.exe</c> to
/// <c>virus.png</c> does not pass.
/// </summary>
public static class AllowedFileTypes
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87 = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89 = "GIF89a"u8.ToArray();
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();

    private static readonly Dictionary<string, FileType> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = new("image/png", ["image/png"], [Png]),
        [".jpg"] = new("image/jpeg", ["image/jpeg", "image/pjpeg"], [Jpeg]),
        [".jpeg"] = new("image/jpeg", ["image/jpeg", "image/pjpeg"], [Jpeg]),
        [".gif"] = new("image/gif", ["image/gif"], [Gif87, Gif89]),
        [".pdf"] = new("application/pdf", ["application/pdf"], [Pdf]),
        [".txt"] = new("text/plain", ["text/plain"], null),
        [".md"] = new("text/markdown", ["text/markdown", "text/x-markdown", "text/plain"], null),
        [".csv"] = new("text/csv", ["text/csv", "text/plain", "application/csv", "application/vnd.ms-excel"], null)
    };

    /// <summary>How many leading bytes of the file are inspected.</summary>
    public const int HeaderLength = 8192;

    public static IReadOnlyCollection<string> Extensions => Types.Keys;

    /// <summary>Canonical MIME type for the extension or <c>null</c> when the extension is not allowed.</summary>
    public static string? ContentTypeFor(string extension) =>
        Types.TryGetValue(extension, out var type) ? type.ContentType : null;

    /// <summary>
    /// A missing or generic (<c>application/octet-stream</c>) client MIME type is tolerated, because many HTTP clients
    /// do not set it; any other value must be one of the types accepted for the extension.
    /// </summary>
    public static bool IsClientContentTypeAcceptable(string extension, string? clientContentType)
    {
        if (!Types.TryGetValue(extension, out var type))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(clientContentType))
        {
            return true;
        }

        var mime = clientContentType.Split(';', 2)[0].Trim();
        return mime.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
               || type.AcceptedClientTypes.Contains(mime, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Checks the first bytes of the file against the expected format of the extension.</summary>
    public static bool ContentMatches(string extension, ReadOnlySpan<byte> header)
    {
        if (!Types.TryGetValue(extension, out var type))
        {
            return false;
        }

        if (type.Signatures is null)
        {
            // Text formats: reject binary content (NUL bytes never occur in UTF-8 / ASCII text).
            return !header.Contains((byte)0);
        }

        foreach (var signature in type.Signatures)
        {
            if (header.StartsWith(signature))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record FileType(string ContentType, string[] AcceptedClientTypes, byte[][]? Signatures);
}
