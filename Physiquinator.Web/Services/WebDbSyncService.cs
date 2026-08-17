using Microsoft.JSInterop;
using SQLite;

namespace Physiquinator.Web.Services;

/// <summary>
/// Periodically exports the on-disk SQLite databases to the browser's IndexedDB so
/// data survives platform restarts with ephemeral filesystems.
/// The browser pushes the copies back on page load (see WebDbRestoreEndpoint).
/// </summary>
public sealed class WebDbSyncService(
    IJSRuntime jsRuntime,
    WebUserContext userContext,
    ILogger<WebDbSyncService> logger)
{
    private readonly string? _dbDirectory = WebDatabasePathProvider.ResolveDatabaseDirectory();

    public async Task SaveAllAsync(CancellationToken cancellationToken)
    {
        if (_dbDirectory is null || !Directory.Exists(_dbDirectory))
            return;

        // Only the current account's databases belong in this browser's IndexedDB.
        foreach (var file in Directory.GetFiles(_dbDirectory, $"physiquinator_{userContext.TenantKey}*.db3"))
        {
            try
            {
                await CheckpointAsync(file);
                var bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                if (bytes.Length == 0)
                    continue;

                await jsRuntime.InvokeVoidAsync(
                    "physiquinatorDb.saveFromServer",
                    cancellationToken,
                    Path.GetFileName(file),
                    Convert.ToBase64String(bytes));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to sync database file {File} to the browser", Path.GetFileName(file));
            }
        }
    }

    /// <summary>Flushes WAL frames into the main file so the exported copy is complete.</summary>
    private static async Task CheckpointAsync(string dbPath)
    {
        var connection = new SQLiteAsyncConnection(dbPath);
        try
        {
            await connection.ExecuteScalarAsync<string>("PRAGMA wal_checkpoint(TRUNCATE)");
        }
        finally
        {
            try
            {
                await connection.CloseAsync();
            }
            catch
            {
                // Ignore close failures; the export continues with whatever was flushed.
            }
        }
    }
}
