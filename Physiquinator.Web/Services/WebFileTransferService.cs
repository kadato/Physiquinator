using Microsoft.JSInterop;
using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>
/// Browser file transfer: exports download straight to the visitor's device and
/// imports open the platform file picker. The JS side lives in wwwroot/js/fileTransfer.js.
/// </summary>
public sealed class WebFileTransferService(IJSRuntime jsRuntime) : IFileTransferService
{
    private const string PickerAccept = ".json,application/json";

    public async Task ExportJsonAsync(string fileName, string json, string shareTitle = "Export Workout Plan")
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("physiquinatorFiles.download", fileName, json);
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone, ignore. Genuine JS failures still bubble to the caller's feedback.
        }
        catch (OperationCanceledException)
        {
            // Operation canceled, ignore.
        }
    }

    public async Task ExportImageAsync(string fileName, byte[] pngBytes, string shareTitle = "Share")
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("physiquinatorFiles.downloadBytes", fileName, Convert.ToBase64String(pngBytes));
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone, ignore. Genuine JS failures still bubble to the caller's feedback.
        }
        catch (OperationCanceledException)
        {
            // Operation canceled, ignore.
        }
    }

    public async Task<string?> PickJsonAsync(string pickerTitle)
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("physiquinatorFiles.pickText", PickerAccept);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }
}
