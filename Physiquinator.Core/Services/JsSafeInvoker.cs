using Microsoft.JSInterop;

namespace Physiquinator.Core.Services;

/// <summary>
/// Wraps IJSRuntime calls that may fail when the circuit or WebView disconnects.
/// Core counterpart to Physiquinator.UI.Services.JsSafeInvoker so Core services do not need a UI reference.
/// </summary>
internal static class JsSafeInvoker
{
    public static async Task InvokeVoidSafeAsync(IJSRuntime js, string identifier, params object?[] args)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, args);
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone, ignore.
        }
        catch (OperationCanceledException)
        {
            // Operation canceled, ignore.
        }
        catch (ObjectDisposedException)
        {
            // Module disposed during teardown.
        }
        catch (InvalidOperationException)
        {
            // JS runtime not available, for example prerendering.
        }
        catch (Exception ex) when (IsJSDisconnected(ex))
        {
            // JSDisconnected via reflection, ignore.
        }
    }

    public static async Task<T?> InvokeSafeAsync<T>(IJSRuntime js, string identifier, params object?[] args)
    {
        try
        {
            return await js.InvokeAsync<T>(identifier, args);
        }
        catch (JSDisconnectedException)
        {
            return default;
        }
        catch (OperationCanceledException)
        {
            return default;
        }
        catch (ObjectDisposedException)
        {
            return default;
        }
        catch (InvalidOperationException)
        {
            return default;
        }
        catch (Exception ex) when (IsJSDisconnected(ex))
        {
            return default;
        }
    }

    private static async Task RunCoreAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone, ignore.
        }
        catch (OperationCanceledException)
        {
            // Operation canceled, ignore.
        }
        catch (ObjectDisposedException)
        {
            // Module disposed during teardown.
        }
        catch (InvalidOperationException)
        {
            // JS runtime not available.
        }
        catch (Exception ex) when (IsJSDisconnected(ex))
        {
            // JSDisconnected via reflection, ignore.
        }
    }

    public static Task RunSafeAsync(Func<Task> action) => RunCoreAsync(action);

    public static ValueTask RunSafeAsync(Func<ValueTask> action) => new(RunCoreAsync(() => action().AsTask()));

    public static bool IsJSDisconnected(Exception ex) =>
        ex.GetType().Name.Contains("JSDisconnected", StringComparison.Ordinal);
}
