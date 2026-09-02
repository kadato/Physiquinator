using Microsoft.JSInterop;
using Physiquinator.Core.Services;

namespace Physiquinator.Wasm.Services;

/// <summary>
/// Browser platform services. In-app updates are native-only and become no-ops
/// here. File transfer uses browser downloads and the file picker. Notifications
/// and vibration use the shared core no-op implementations.
/// </summary>
public sealed class WasmNoopUpdateService : IAppUpdateService
{
    private static readonly Version Version = new(1, 0, 0);

    public Version CurrentVersion => Version;

    public bool IsSupported => false;

    public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new UpdateCheckResult(null, false, null));

    public Task DownloadAndInstallAsync(UpdateCheckResult update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class WasmFileTransferService(IJSRuntime js) : IFileTransferService
{
    /// <summary>Triggers a client-side download of the given bytes.</summary>
    private async Task DownloadAsync(string fileName, string mimeType, byte[] bytes)
    {
        try
        {
            await js.InvokeVoidAsync(
                "physiquinatorWasm.downloadFile",
                fileName,
                mimeType,
                Convert.ToBase64String(bytes));
        }
        catch (JSException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Download failed: {ex.Message}");
        }
        catch (JSDisconnectedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Download disconnected: {ex.Message}");
        }
    }

    public async Task ExportJsonAsync(string fileName, string json, string shareTitle = "Export Workout Plan")
        => await DownloadAsync(fileName, "application/json", System.Text.Encoding.UTF8.GetBytes(json));

    public async Task ExportImageAsync(string fileName, byte[] pngBytes, string shareTitle = "Share")
        => await DownloadAsync(fileName, "image/png", pngBytes);

    public async Task<string?> PickJsonAsync(string pickerTitle)
    {
        try
        {
            return await js.InvokeAsync<string?>("physiquinatorWasm.pickJson", pickerTitle);
        }
        catch (JSException)
        {
            return null;
        }
    }
}
