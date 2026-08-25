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

    public Task ExportJsonAsync(string fileName, string json, string shareTitle = "Export Workout Plan") =>
        jsRuntime.InvokeVoidAsync("physiquinatorFiles.download", fileName, json).AsTask();

    public Task ExportImageAsync(string fileName, byte[] pngBytes, string shareTitle = "Share") =>
        jsRuntime.InvokeVoidAsync("physiquinatorFiles.downloadBytes", fileName, Convert.ToBase64String(pngBytes)).AsTask();

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
