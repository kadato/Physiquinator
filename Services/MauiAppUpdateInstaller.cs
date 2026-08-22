using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using System.Diagnostics;
using System.IO.Compression;

#if ANDROID
using Android.Content;
#endif

namespace Physiquinator.Services;

/// <summary>
/// Downloads and installs Physiquinator releases on the current platform.
/// Android launches the APK installer via a content intent. Windows extracts the ZIP,
/// replaces the running app folder from a wait-and-restart updater script, then exits the app.
/// </summary>
public sealed class MauiAppUpdateInstaller : IAppUpdateInstaller
{
#if ANDROID || WINDOWS
    private readonly HttpClient _http;
#endif

    public MauiAppUpdateInstaller(HttpClient http)
    {
#if ANDROID || WINDOWS
        _http = http;
#endif
    }

    /// <inheritdoc />
    public bool IsSupported => DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.WinUI;

    /// <inheritdoc />
    public string AssetFileName
    {
        get
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                return GitHubReleaseAssets.AndroidApk;
            }

            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                return GitHubReleaseAssets.WindowsZip;
            }

            return string.Empty;
        }
    }

    /// <inheritdoc />
    public Task InstallAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
#if ANDROID
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return InstallAndroidAsync(downloadUrl, progress, cancellationToken);
        }
#endif
#if WINDOWS
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return InstallWindowsAsync(downloadUrl, progress, cancellationToken);
        }
#endif
        throw new NotSupportedException("In-app updates are not supported on this platform.");
    }

#if ANDROID
    private async Task InstallAndroidAsync(string downloadUrl, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var apkPath = Path.Combine(FileSystem.CacheDirectory, GitHubReleaseAssets.AndroidApk);
        await DownloadAsync(downloadUrl, apkPath, progress, cancellationToken);
        LaunchApkInstaller(apkPath);
    }
#endif

#if WINDOWS
    private async Task InstallWindowsAsync(string downloadUrl, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var updateRoot = Path.Combine(Path.GetTempPath(), "PhysiquinatorUpdate");
        Directory.CreateDirectory(updateRoot);
        var zipPath = Path.Combine(updateRoot, GitHubReleaseAssets.WindowsZip);
        await DownloadAsync(downloadUrl, zipPath, progress, cancellationToken);
        LaunchWindowsUpdater(zipPath, updateRoot);
    }
#endif

#if ANDROID || WINDOWS
    private async Task DownloadAsync(string url, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);

        try
        {
            await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            long copied = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                copied += read;
                if (totalBytes is > 0)
                {
                    progress?.Report((double)copied / totalBytes.Value);
                }
            }
        }
        catch
        {
            if (File.Exists(destinationPath))
            {
                try { File.Delete(destinationPath); } catch { }
            }
            throw;
        }
    }
#endif

#if ANDROID
    private static void LaunchApkInstaller(string apkPath)
    {
        Android.Content.Context context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
        var file = new Java.IO.File(apkPath);

        if (!file.Exists())
        {
            throw new FileNotFoundException("Downloaded APK file not found.", apkPath);
        }

        // Check for Android 8.0+ (API 26) unknown app sources install permission
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && context.PackageManager != null && !context.PackageManager.CanRequestPackageInstalls())
        {
            var settingsIntent = new Intent(Android.Provider.Settings.ActionManageUnknownAppSources);
            settingsIntent.SetData(Android.Net.Uri.Parse($"package:{context.PackageName}"));
            settingsIntent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(settingsIntent);
            throw new InvalidOperationException("Installation permission required. Please enable 'Allow from this source' for Physiquinator in Android settings and tap Install again.");
        }

        Android.Net.Uri uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
            context,
            $"{context.PackageName}.fileprovider",
            file)!;

        var intent = new Android.Content.Intent(Intent.ActionView);
        intent.SetDataAndType(uri, "application/vnd.android.package-archive");
        intent.SetFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
        context.StartActivity(intent);
    }
#endif

#if WINDOWS
    private static void LaunchWindowsUpdater(string zipPath, string updateRoot)
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var appExe = Path.Combine(exeDir, "Physiquinator.exe");

        var staging = Path.Combine(updateRoot, "staging");
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
        ZipFile.ExtractToDirectory(zipPath, staging);
        EnsureWritable(exeDir, zipPath);

        var updaterPath = Path.Combine(updateRoot, "update.cmd");
        File.WriteAllText(updaterPath, BuildUpdaterScript(exeDir, appExe), System.Text.Encoding.ASCII);

        Process.Start(new ProcessStartInfo
        {
            FileName = updaterPath,
            WorkingDirectory = updateRoot,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Microsoft.Maui.Controls.Application.Current?.Quit();
    }

    private static string BuildUpdaterScript(string exeDir, string appExe)
    {
        return $"""
            @echo off
            setlocal
            cd /d "%~dp0"
            :wait
            tasklist /FI "IMAGENAME eq Physiquinator.exe" 2>nul | find /I "Physiquinator.exe" >nul
            if not errorlevel 1 (
              timeout /t 1 /nobreak >nul
              goto wait
            )
            robocopy "%~dp0staging" "{exeDir}" /E /IS /IT /R:2 /W:2 /NFL /NDL /NJH /NJS >nul
            if errorlevel 8 goto done
            rmdir /s /q "%~dp0staging"
            :done
            start "" "{appExe}"
            exit /b 0
            """;
    }

    private static void EnsureWritable(string exeDir, string zipPath)
    {
        var probe = Path.Combine(exeDir, ".physiquinator-write-probe");
        try
        {
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"The app folder is not writable ({exeDir}). The update ZIP is saved at {zipPath}; extract it over the app folder manually.",
                ex);
        }
    }
#endif
}
