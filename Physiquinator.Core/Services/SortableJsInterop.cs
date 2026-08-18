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
            // Ensure Sortable.js is loaded before calling the init function.
            // The script is lazy-loaded on demand to avoid blocking initial page load.
            var sortableReady = await js.InvokeAsync<bool>(
                "eval", "typeof Sortable !== 'undefined'");
            if (!sortableReady)
            {
                await js.InvokeVoidAsync("eval",
                    "new Promise((resolve, reject) => {" +
                    "var s = document.createElement('script');" +
                    "s.src = '_content/Physiquinator.UI/js/sortable.min.js';" +
                    "s.onload = resolve; s.onerror = reject;" +
                    "document.head.appendChild(s);" +
                    "})");
            }
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
