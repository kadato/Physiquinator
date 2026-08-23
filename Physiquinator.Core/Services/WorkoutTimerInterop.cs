using Microsoft.JSInterop;

namespace Physiquinator.Core.Services;

/// <summary>
/// Wraps the workout rest-timer JS module (<c>workoutTimer.js</c>): tick timer,
/// audio unlock, and completion sounds. Swallows teardown-related JS exceptions.
/// Scoped, so the same instance is shared across page navigations within a
/// circuit: the workout page calls <see cref="DisposeAsync"/> when the user
/// leaves, and <see cref="InitializeAsync"/> re-uses the instance (and its
/// module) on the next mount.
/// </summary>
public sealed class WorkoutTimerInterop(IJSRuntime js) : IAsyncDisposable
{
    // Cache-busting query: the module's progress-bar logic evolved. Older
    // WebView caches of the module would show a stale bar.
    private const string ModulePath = "./_content/Physiquinator.UI/js/workoutTimer.js?v=8";

    private IJSObjectReference? _module;

    public async ValueTask InitializeAsync()
    {
        if (_module != null)
            return;

        await InvokeSafeAsync(async () =>
        {
            _module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        });
    }

    /// <param name="remainingMs">Seconds left in the active rest.</param>
    /// <param name="activeDurationMs">Full interval of the active rest (for progress fraction).</param>
    /// <param name="continueMode">True when the rest is extended or synced (bar continues from its
    /// current position), false for a fresh rest or reset (bar restarts).</param>
    public Task StartTimerAsync<T>(DotNetObjectReference<T> dotNetRef, int tickIntervalMs, int remainingMs, int activeDurationMs, bool continueMode)
        where T : class =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("startRestTimer", dotNetRef, tickIntervalMs, remainingMs, activeDurationMs, continueMode));

    public Task StopTimerAsync() =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("stopRestTimer"));

    public Task UnlockAudioAsync() =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("unlockAudioContext"));

    public Task PlayRestCompleteSoundAsync() =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("playRestCompleteSound"));

    public Task RegisterUndoKeyHandlerAsync<T>(DotNetObjectReference<T> dotNetRef)
        where T : class =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("registerUndoKeyHandler", dotNetRef));

    public Task SetKeepScreenOnAsync(bool enabled) =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("setKeepScreenOn", enabled));

    public Task RegisterBackHandlerAsync<T>(DotNetObjectReference<T> dotNetRef)
        where T : class =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("registerBackHandler", dotNetRef));

    public Task UnregisterBackHandlerAsync() =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("unregisterBackHandler"));

    public async ValueTask DisposeAsync()
    {
        // Stops the JS timer chain and global handlers so nothing keeps
        // ticking a stale DotNetObjectReference after the page is gone. These
        // calls must not be swallowed by any guard: the module itself stays
        // alive because this scoped service is reused on the next workout
        // page mount.
        if (_module == null)
            return;

        await InvokeModuleAsync(module => module.InvokeVoidAsync("unregisterUndoKeyHandler"));
        await InvokeModuleAsync(module => module.InvokeVoidAsync("setKeepScreenOn", false));
        await InvokeModuleAsync(module => module.InvokeVoidAsync("unregisterBackHandler"));
        await InvokeModuleAsync(module => module.InvokeVoidAsync("stopRestTimer"));
    }

    private Task InvokeModuleAsync(Func<IJSObjectReference, ValueTask> action) =>
        InvokeSafeAsync(() =>
        {
            if (_module != null)
                return action(_module).AsTask();
            return Task.CompletedTask;
        });

    /// <summary>Runs a JS call, swallowing exceptions raised during WebView teardown.</summary>
    private static async Task InvokeSafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (JSDisconnectedException)
        {
            // WebView or JS runtime already torn down
        }
        catch (ObjectDisposedException)
        {
            // Module reference disposed during teardown
        }
        catch (InvalidOperationException)
        {
            // JS runtime not available, for example prerendering.
        }
    }
}
