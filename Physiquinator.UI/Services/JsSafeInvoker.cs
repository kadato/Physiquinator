using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Physiquinator.UI.Services;

/// <summary>
/// Wraps IJSRuntime calls that may fail when the circuit or WebView disconnects.
/// Central place so pages do not repeat JSDisconnectedException catch blocks.
/// </summary>
public static class JsSafeInvoker
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
			// Operation canceled.
			return default;
		}
		catch (Exception ex) when (IsJSDisconnected(ex))
		{
			// JSDisconnected via reflection.
			return default;
		}
	}

	public static async Task<bool> TryCopyTextAsync(IJSRuntime js, string text, string helper = "physiquinatorHelpers.copyText")
	{
		var result = await InvokeSafeAsync<bool>(js, helper, text);
		return result;
	}

	public static async Task ScrollToBottomAsync(IJSRuntime js, ElementReference element, string helper = "physiquinatorHelpers.scrollToBottom")
	{
		await InvokeVoidSafeAsync(js, helper, element);
	}

	private static bool IsJSDisconnected(Exception ex) =>
		ex.GetType().Name.Contains("JSDisconnected", StringComparison.Ordinal);
}
