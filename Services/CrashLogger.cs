namespace Physiquinator.Services;

/// <summary>
/// Writes unhandled exceptions to a persistent log file so crashes can be
/// diagnosed from a release build without requiring ADB.
/// </summary>
public static class CrashLogger
{
    private static readonly string LogPath =
        Path.Combine(FileSystem.AppDataDirectory, "crash.log");

    private static readonly SemaphoreSlim _lock = new(1, 1);

    public static void Log(string source, Exception? ex)
    {
        var entry = BuildEntry(source, ex?.ToString() ?? "(no exception)");
        WriteEntry(entry);
    }

    public static void Log(string source, string message)
    {
        var entry = BuildEntry(source, message);
        WriteEntry(entry);
    }

    /// <summary>Returns the full path to the crash log file, or null if it does not exist.</summary>
    public static string? LogFilePath => File.Exists(LogPath) ? LogPath : null;

    /// <summary>Deletes the log file.</summary>
    public static void Clear()
    {
        try { File.Delete(LogPath); } catch { }
    }

    private static string BuildEntry(string source, string detail) =>
        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}]{Environment.NewLine}{detail}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";

    private static void WriteEntry(string entry)
    {
        // Fire-and-forget; never propagate exceptions from the logger itself.
        _ = Task.Run(async () =>
        {
            await _lock.WaitAsync();
            try { await File.AppendAllTextAsync(LogPath, entry); }
            catch { }
            finally { _lock.Release(); }
        });
    }
}
