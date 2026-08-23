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

    public async Task<IReadOnlyList<string>> RestoreAllAsync()
    {
        try
        {
            var module = await GetModuleAsync();
            string[] names = await module.InvokeAsync<string[]>("listDatabases");
            foreach (var name in names)
            {
                byte[]? bytes = await module.InvokeAsync<byte[]?>("loadDatabase", name);
                if (bytes == null || bytes.Length == 0)
                {
                    continue;
                }
                await File.WriteAllBytesAsync(name, bytes);
                logger.LogInformation("Restored {Name} from Cache Storage ({Length} bytes)", name, bytes.Length);
            }
            return names;
        }
        catch (Exception ex)
        {
            // First visit has no cache yet. A broken restore must never brick the app.
            logger.LogWarning(ex, "Database restore skipped");
            return [];
        }
    }

    /// <summary>Writes every wasm-filesystem database file back into Cache Storage.</summary>
    public async Task SaveAllAsync()
    {
        try
        {
            // Databases run in WAL mode: recent commits sit in -wal sidecars
            // until a checkpoint. Fold them into the main file first so the
            // exported bytes include everything. Temporary connections are
            // used deliberately: resolving the app's own AppDatabase here
            // would construct it before the boot-time restore has run.
            var module = await GetModuleAsync();
            var files = Directory.GetFiles(".", "*.db3");
            foreach (var file in files)
            {
                try
                {
                    var checkpointConnection = new SQLite.SQLiteAsyncConnection(file);
                    await checkpointConnection.ExecuteAsync("PRAGMA wal_checkpoint(TRUNCATE);");
                    await checkpointConnection.CloseAsync();
                }
                catch
                {
                    // Export proceeds with the last checkpointed state.
                }

                byte[] bytes = await File.ReadAllBytesAsync(file);
                await module.InvokeVoidAsync("saveDatabase", Path.GetFileName(file), bytes);
                logger.LogInformation("Exported {Name} to Cache Storage ({Length} bytes)", Path.GetFileName(file), bytes.Length);
            }
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
    public async Task InstallPageHideSaveHookAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerPageHide", DotNetObjectReference.Create(new PageHideCallback(this)));
    }

    private sealed class PageHideCallback(WasmDbPersistence owner)
    {
        [JSInvokable]
        public Task OnPageHide() => owner.SaveAllAsync();
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        if (_module == null)
        {
            _module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        }
        return _module;
    }
}
