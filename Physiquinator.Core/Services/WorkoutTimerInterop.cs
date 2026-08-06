using Microsoft.JSInterop;

namespace Physiquinator.Core.Services;

/// <summary>
/// Wraps the workout rest-timer JS module (<c>workoutTimer.js</c>): tick timer,
/// audio unlock, and completion sounds. Swallows teardown-related JS exceptions.
/// </summary>
public sealed class WorkoutTimerInterop(IJSRuntime js) : IAsyncDisposable
{
    private const string ModulePath = "./_content/Physiquinator.UI/js/workoutTimer.js";

    private IJSObjectReference? _module;
    private bool _disposed;

    public async ValueTask InitializeAsync()
    {
        if (_module != null)
            return;

        await InvokeSafeAsync(async () =>
        {
            _module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        });
    }

    public Task StartTimerAsync<T>(DotNetObjectReference<T> dotNetRef, int tickIntervalMs, int totalMs)
        where T : class =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("startRestTimer", dotNetRef, tickIntervalMs, totalMs));

    public Task StopTimerAsync() =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("stopRestTimer"));

    public Task UnlockAudioAsync() =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("unlockAudioContext"));

    public Task PlayRestCompleteSoundAsync() =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("playRestCompleteSound"));

    public Task RegisterUndoKeyHandlerAsync<T>(DotNetObjectReference<T> dotNetRef)
        where T : class =>
        InvokeModuleAsync(module => module.InvokeVoidAsync("registerUndoKeyHandler", dotNetRef));

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await InvokeModuleAsync(module => module.InvokeVoidAsync("unregisterUndoKeyHandler"));
        await InvokeModuleAsync(module => module.InvokeVoidAsync("stopRestTimer"));
        if (_module != null)
        {
            await _module.DisposeAsync();
            _module = null;
        }
    }

    private Task InvokeModuleAsync(Func<IJSObjectReference, ValueTask> action) =>
        InvokeSafeAsync(() =>
        {
            if (_module != null)
                return action(_module).AsTask();
            return Task.CompletedTask;
        });

    /// <summary>Runs a JS call, swallowing exceptions raised during WebView teardown.</summary>
    private async Task InvokeSafeAsync(Func<Task> action)
    {
        if (_disposed)
            return;

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
            // JS runtime not available (e.g. prerendering)
        }
    }
}
