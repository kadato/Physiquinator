namespace Physiquinator.Core.Services;

/// <summary>
/// Reads the PHYSIQUINATOR_* environment variables once for the whole process.
/// Used by the MAUI host and tooling to switch between normal and screenshot mode.
/// </summary>
public static class AppEnvironment
{
    /// <summary>True when PHYSIQUINATOR_SCREENSHOT_MODE is set to "true".</summary>
    public static bool IsScreenshotMode { get; } =
        Environment.GetEnvironmentVariable("PHYSIQUINATOR_SCREENSHOT_MODE") == "true";

    /// <summary>Chrome DevTools port used in screenshot mode, from PHYSIQUINATOR_CDP_PORT.</summary>
    public static int ScreenshotCdpPort { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("PHYSIQUINATOR_CDP_PORT"), out var port) ? port : 9255;

    /// <summary>Optional PHYSIQUINATOR_DB_DIR override for the database directory.</summary>
    public static string? DatabaseDirectoryOverride { get; } =
        Environment.GetEnvironmentVariable("PHYSIQUINATOR_DB_DIR");
}
