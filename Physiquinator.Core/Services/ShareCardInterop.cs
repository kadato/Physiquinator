using Microsoft.JSInterop;

namespace Physiquinator.Core.Services;

/// <summary>
/// Captures a rendered DOM element as a PNG byte array for image sharing,
/// using the vendored html2canvas library (loaded globally by the host shell).
/// Swallows teardown-related JS exceptions like the other interop wrappers.
/// </summary>
public sealed class ShareCardInterop(IJSRuntime js)
{
    private const string ModulePath = "./_content/Physiquinator.UI/js/shareCard.js";

    private IJSObjectReference? _module;

    public async Task<byte[]?> CaptureElementAsPngAsync(string selector)
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);

            var dataUrl = await _module.InvokeAsync<string>("ShareCard.capture", selector);
            if (string.IsNullOrWhiteSpace(dataUrl))
                return null;

            const string prefix = "data:image/png;base64,";
            if (!dataUrl.StartsWith(prefix, StringComparison.Ordinal))
                return null;

            return Convert.FromBase64String(dataUrl[prefix.Length..]);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
