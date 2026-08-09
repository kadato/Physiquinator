using System.Buffers;
using System.Text;

namespace Physiquinator.Core.Services;

/// <summary>Host-independent JSON file naming helpers.</summary>
public static class FileTransferNames
{
    private static readonly SearchValues<char> s_invalidFileNameChars =
        SearchValues.Create(Path.GetInvalidFileNameChars());

    /// <summary>Replaces characters that are invalid in file names with underscores.</summary>
    public static string SafeFileStem(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var result = new StringBuilder(name.Length);
        ReadOnlySpan<char> remaining = name.AsSpan();
        while (!remaining.IsEmpty)
        {
            var invalidIndex = remaining.IndexOfAny(s_invalidFileNameChars);
            if (invalidIndex < 0)
            {
                result.Append(remaining);
                break;
            }
            result.Append(remaining[..invalidIndex]).Append('_');
            remaining = remaining[(invalidIndex + 1)..];
        }
        return result.ToString();
    }

    /// <summary>Builds a timestamped JSON file name from a stem, e.g. "Push Day" → "Push_Day_20260101_120000.json".</summary>
    public static string JsonFileName(string stem, DateTimeOffset localNow) =>
        $"{stem}_{localNow:yyyyMMdd_HHmmss}.json";

    /// <summary>Builds a timestamped PNG file name from a stem, e.g. "Push Day" → "Push_Day_20260101_120000.png".</summary>
    public static string PngFileName(string stem, DateTimeOffset localNow) =>
        $"{stem}_{localNow:yyyyMMdd_HHmmss}.png";
}
