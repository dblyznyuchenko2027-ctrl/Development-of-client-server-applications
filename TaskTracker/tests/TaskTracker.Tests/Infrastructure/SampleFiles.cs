namespace TaskTracker.Tests.Infrastructure;

/// <summary>Small valid files used by the upload tests.</summary>
public static class SampleFiles
{
    /// <summary>A real 1x1 PNG image.</summary>
    public static byte[] Png { get; } = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public static byte[] Pdf { get; } = "%PDF-1.4\n%integration test\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF\n"u8.ToArray();

    public static byte[] Text { get; } = "Hello, TaskTracker!\n"u8.ToArray();

    /// <summary>Starts with a PNG signature so that only the size check can reject it.</summary>
    public static byte[] PngOfSize(int length)
    {
        var bytes = new byte[length];
        Png.AsSpan(0, 8).CopyTo(bytes);
        return bytes;
    }

    /// <summary>Windows executable header: "MZ".</summary>
    public static byte[] Executable { get; } = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
}
