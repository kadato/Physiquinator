using Microsoft.JSInterop;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;

namespace Physiquinator.Wasm.Services;

/// <summary>
/// Browser platform services. Notifications, vibration, overlay timers, and
/// in-app updates are native-only features and become no-ops here. The rest
/// timer keeps running inside the page exactly like the desktop host.
/// File transfer uses browser downloads and the file picker.
/// </summary>
public sealed class WasmNoopNotificationService : INotificationService
{
    public Task EnsurePermissionAsync() => Task.CompletedTask;

    public bool SupportsNotifications => false;

    public bool SupportsOverlay => false;

    public bool HasOverlayPermission() => false;

    public Task RequestOverlayPermissionAsync() => Task.CompletedTask;

    public void CancelAllRestNotifications()
    {
    }

    public Task ShowRestCompleteNowAsync(string description) => Task.CompletedTask;

    public Task ShowWorkoutTimerUiAsync(WorkoutTimerState state) => Task.CompletedTask;

    public Task HideWorkoutTimerUiAsync() => Task.CompletedTask;

    public Task ScheduleRestEndAlarmAsync(DateTime restEndsAtUtc, string title, string description) => Task.CompletedTask;

    public Task CancelRestEndAlarmAsync() => Task.CompletedTask;
}

public sealed class WasmNoopVibrationService : IVibrationService
{
    public void Vibrate(TimeSpan duration)
    {
    }
}

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
