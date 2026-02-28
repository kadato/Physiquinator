namespace Physiquinator.Services;

/// <summary>
/// Writes crash details and breadcrumbs to a persistent log file so crashes
/// can be diagnosed from a release build without requiring ADB.
///
/// All writes are synchronous so entries survive even inside AppDomain.UnhandledException
/// handlers, where the process may exit immediately after the handler returns.
/// </summary>
public static class CrashLogger
{
    private static readonly string LogPath =
        Path.Combine(FileSystem.AppDataDirectory, "crash.log");

    // Plain object lock — safe to use on any thread including the thread-pool
    // callbacks where Timer.Elapsed fires.
    private static readonly object _fileLock = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Logs a caught exception with a source tag.</summary>
    public static void Log(string source, Exception? ex)
    {
        var body = ex?.ToString() ?? "(no exception)";
        WriteEntry($"[{Now}] [ERROR/{source}]{Environment.NewLine}{body}{Environment.NewLine}{Bar}");
    }

    /// <summary>Logs a plain message with a source tag.</summary>
    public static void Log(string source, string message)
    {
        WriteEntry($"[{Now}] [ERROR/{source}]{Environment.NewLine}{message}{Environment.NewLine}{Bar}");
    }

    /// <summary>
    /// Writes a one-line trace entry. Call this at key checkpoints (app start,
    /// button press, timer start, etc.) so the file always shows what was
    /// happening just before a native crash that bypasses all .NET handlers.
    /// </summary>
    public static void Breadcrumb(string message)
    {
        WriteEntry($"[{Now}] {message}{Environment.NewLine}");
    }

    /// <summary>Returns the full path to the log file, or null when it does not exist.</summary>
    public static string? LogFilePath => File.Exists(LogPath) ? LogPath : null;

    /// <summary>Deletes the log file.</summary>
    public static void Clear()
    {
        try { File.Delete(LogPath); } catch { }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static string Now => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
    private static string Bar => new('-', 80);

    private static void WriteEntry(string entry)
    {
        // Synchronous write so the entry is on disk before this method returns.
        // This is essential inside UnhandledException where the process exits
        // immediately after the handler finishes.
        lock (_fileLock)
        {
            try { File.AppendAllText(LogPath, entry); }
            catch { /* never let the logger itself crash the app */ }
        }
    }
}
