#pragma warning disable S5034 // ValueTask consumed once via AsTask().WaitAsync, false positive
using Microsoft.JSInterop;
using Physiquinator.Core.Data;

namespace Physiquinator.Wasm.Services;

/// <summary>
/// Persists database files between WebAssembly sessions through Cache Storage
/// (wwwroot/js/wasm-persist.js). RestoreAllAsync runs during app boot, before
/// any AppDatabase opens a connection. SaveAsync is called on a timer and on
/// pagehide so the browser always holds the newest bytes.
/// </summary>
public sealed class WasmDbPersistence(IJSRuntime js, ILogger<WasmDbPersistence> logger)
{
    private const string ModulePath = "./js/wasm-persist.js";
    private IJSObjectReference? _module;

    public async Task<IReadOnlyList<string>> RestoreAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await GetModuleAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
            var names = await module.InvokeAsync<string[]>("listDatabases", cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
            foreach (var name in names)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytes = await module.InvokeAsync<byte[]?>("loadDatabase", cancellationToken, name).AsTask().WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                if (bytes == null || bytes.Length == 0)
                {
                    continue;
                }
                await File.WriteAllBytesAsync(name, bytes, cancellationToken);
                logger.LogInformation("Restored {Name} from Cache Storage ({Length} bytes)", name, bytes.Length);
            }
            return names;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // First visit has no cache yet. A broken restore must never brick the app.
            logger.LogWarning(ex, "Database restore skipped");
            return [];
        }
    }

    /// <summary>Writes every wasm-filesystem database file back into Cache Storage.</summary>
    public async Task SaveAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Databases run in WAL mode: recent commits sit in -wal sidecars
            // until a checkpoint. Fold them into the main file first so the
            // exported bytes include everything. Temporary connections are
            // used deliberately: resolving the app's own AppDatabase here
            // would construct it before the boot-time restore has run.
            var module = await GetModuleAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
            var files = Directory.GetFiles(".", "*.db3");
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var checkpointConnection = new SQLite.SQLiteAsyncConnection(file);
                    await checkpointConnection.ExecuteAsync("PRAGMA wal_checkpoint(TRUNCATE);").WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
                    await checkpointConnection.CloseAsync();
                }
                catch (Exception ex)
                {
                    // Export proceeds with the last checkpointed state.
                    logger.LogDebug(ex, "Checkpoint skipped for {File}", file);
                }

                var bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                await module.InvokeVoidAsync("saveDatabase", cancellationToken, Path.GetFileName(file), bytes).AsTask().WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                logger.LogInformation("Exported {Name} to Cache Storage ({Length} bytes)", Path.GetFileName(file), bytes.Length);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database save failed");
        }
    }

    /// <summary>
    /// Installs pagehide/visibilitychange hooks that push database bytes into
    /// Cache Storage when the user leaves or hides the tab.
    /// </summary>
    public async Task InstallPageHideSaveHookAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await GetModuleAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
            await module.InvokeVoidAsync("registerPageHide", cancellationToken, DotNetObjectReference.Create(new PageHideCallback(this))).AsTask().WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
        }
        catch (Exception ex)
        {
            // Best effort only. A broken hook must never brick the boot gate.
            logger.LogWarning(ex, "PageHide hook install skipped");
        }
    }

    private sealed class PageHideCallback(WasmDbPersistence owner)
    {
        [JSInvokable]
        public Task OnPageHide() => owner.SaveAllAsync();
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await GetModuleAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
            await module.InvokeVoidAsync("clearAll", cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
            logger.LogInformation("Cleared Cache Storage");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Clear Cache Storage failed");
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken = default)
    {
        if (_module != null)
        {
            return _module;
        }

        try
        {
            _module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException("Failed to import wasm-persist module", ex);
        }
        catch (JSDisconnectedException ex)
        {
            throw new InvalidOperationException("JS disconnected while importing wasm-persist module", ex);
        }

        return _module;
    }
}
#pragma warning restore S5034
