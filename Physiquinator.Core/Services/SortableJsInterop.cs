using Microsoft.JSInterop;

namespace Physiquinator.Core.Services;

/// <summary>
/// Shared init/destroy plumbing for the drag-sort JS modules (home plan list and plan exercise list).
/// Both modules follow the same contract: destroy first, then init(listId, dotNetRef), returning success.
/// </summary>
public static class SortableJsInterop
{
    public static async ValueTask<bool> TryInitAsync<T>(
        IJSRuntime js,
        string listId,
        DotNetObjectReference<T> dotNetRef,
        string initFunction,
        string destroyFunction)
        where T : class
    {
        try
        {
            await DestroyAsync(js, destroyFunction);
            return await js.InvokeAsync<bool>(initFunction, listId, dotNetRef);
        }
        catch (JSException)
        {
            return false;
        }
    }

    public static async ValueTask DestroyAsync(IJSRuntime js, string destroyFunction)
    {
        try
        {
            await js.InvokeVoidAsync(destroyFunction);
        }
        catch (JSException)
        {
            // Scripts not ready yet
        }
    }
}
