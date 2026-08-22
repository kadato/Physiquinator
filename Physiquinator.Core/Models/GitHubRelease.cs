namespace Physiquinator.Core.Models;

/// <summary>A downloadable asset attached to a GitHub release.</summary>
public sealed record GitHubReleaseAsset(string Name, string DownloadUrl, long SizeBytes);

/// <summary>Release metadata parsed from the GitHub releases API.</summary>
public sealed record GitHubRelease(
    string Tag,
    string Name,
    string? Notes,
    DateTimeOffset? PublishedAt,
    bool IsPrerelease,
    IReadOnlyList<GitHubReleaseAsset> Assets)
{
    /// <summary>Version parsed from the release tag (for example, "v1.2.0" to 1.2.0), or null when the tag is not a version.</summary>
    public Version? Version => Version.TryParse(Tag.TrimStart('v', 'V'), out Version? parsed) ? parsed : null;

    /// <summary>True when this release is newer than the installed version.</summary>
    public bool IsNewerThan(Version installed) => Version is { } release && release > installed;
}

/// <summary>Well-known asset file names published by the release pipeline.</summary>
public static class GitHubReleaseAssets
{
    public const string AndroidApk = "Physiquinator-Android.apk";
    public const string WindowsZip = "Physiquinator-Windows.zip";
}
