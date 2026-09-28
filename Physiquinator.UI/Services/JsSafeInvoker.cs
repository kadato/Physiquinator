using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Physiquinator.UI.Services;

/// <summary>
/// UI facade over the shared core invoker. Extra helpers live here,
/// disconnect handling lives in Core so the two cannot drift.
/// </summary>
public static class JsSafeInvoker
{
    public static Task InvokeVoidSafeAsync(IJSRuntime js, string identifier, params object?[] args) =>
        Core.Services.JsInteropGuard.InvokeVoidSafeAsync(js, identifier, args);

    public static Task<T?> InvokeSafeAsync<T>(IJSRuntime js, string identifier, params object?[] args) =>
        Core.Services.JsInteropGuard.InvokeSafeAsync<T>(js, identifier, args);

    public static async Task<bool> TryCopyTextAsync(IJSRuntime js, string text, string helper = "physiquinatorHelpers.copyText")
    {
        var result = await InvokeSafeAsync<bool>(js, helper, text);
        return result;
    }

    public static async Task ScrollToBottomAsync(IJSRuntime js, ElementReference element, string helper = "physiquinatorHelpers.scrollToBottom") => await InvokeVoidSafeAsync(js, helper, element);

    public static Task RunSafeAsync(Func<Task> action) =>
        Core.Services.JsInteropGuard.RunSafeAsync(action);

    public static ValueTask RunSafeAsync(Func<ValueTask> action) =>
        Core.Services.JsInteropGuard.RunSafeAsync(action);

    public static bool IsJSDisconnected(Exception ex) =>
        Core.Services.JsInteropGuard.IsJSDisconnected(ex);
}
