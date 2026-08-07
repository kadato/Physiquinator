using Microsoft.JSInterop;

namespace Physiquinator.UI.Services;

/// <summary>
/// Observes a sentinel element with an IntersectionObserver and invokes a
/// .NET method whenever it scrolls into view. The JS side re-arms itself
/// while the method reports that more items exist (infinite scroll).
/// </summary>
public static class InfiniteScrollInterop
{
    private const string ModulePath = "./_content/Physiquinator.UI/js/infiniteScroll.js";

    public static async ValueTask ObserveAsync<T>(IJSRuntime js, string sentinelId, DotNetObjectReference<T> dotNetRef, string methodName)
        where T : class
    {
        var module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        await module.InvokeVoidAsync("observe", sentinelId, dotNetRef, methodName);
        await module.DisposeAsync();
    }

    public static async ValueTask DisposeAsync(IJSRuntime js, string sentinelId)
    {
        var module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        await module.InvokeVoidAsync("dispose", sentinelId);
        await module.DisposeAsync();
    }
}
