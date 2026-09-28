using Microsoft.Extensions.Logging;
using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>Shared update-check logic. Downloading and installing is delegated to the platform installer.</summary>
public sealed class AppUpdateService(IGitHubReleaseClient client, IAppUpdateInstaller installer, Version currentVersion, ILogger<AppUpdateService>? logger = null) : IAppUpdateService
{
    private readonly IGitHubReleaseClient _client = client;
    private readonly IAppUpdateInstaller _installer = installer;

    /// <inheritdoc />
    public Version CurrentVersion { get; } = currentVersion;

    /// <inheritdoc />
    public bool IsSupported => _installer.IsSupported;

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        GitHubRelease? release;
        try
        {
            release = await _client.GetLatestReleaseAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Update check failed, treating as no update.");
            return new UpdateCheckResult(null, false, null);
        }

        if (release is null)
        {
            return new UpdateCheckResult(null, false, null);
        }

        var isUpdateAvailable = release.IsNewerThan(CurrentVersion);
        if (!isUpdateAvailable)
        {
            return new UpdateCheckResult(release, false, null);
        }

        GitHubReleaseAsset? asset = null;
        if (_installer.AssetFileName is { Length: > 0 })
        {
            asset = release.Assets.FirstOrDefault(a => string.Equals(a.Name, _installer.AssetFileName, StringComparison.OrdinalIgnoreCase));
            if (asset is null)
            {
                var extension = Path.GetExtension(_installer.AssetFileName);
                if (string.Equals(extension, ".apk", StringComparison.OrdinalIgnoreCase))
                {
                    asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));
                }
                else if (!string.IsNullOrEmpty(extension))
                {
                    asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) &&
                        (a.Name.Contains("physiquinator", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("windows", StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        var downloadUrl = asset?.DownloadUrl;
        if (!string.IsNullOrWhiteSpace(downloadUrl) && !IsHttpsUrl(downloadUrl))
        {
            logger?.LogWarning("Ignoring non-HTTPS update URL for asset {AssetName}.", asset?.Name);
            downloadUrl = null;
        }

        return new UpdateCheckResult(release, true, downloadUrl);
    }

    private static bool IsHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    /// <inheritdoc />
    public async Task DownloadAndInstallAsync(UpdateCheckResult update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (update.DownloadUrl is null)
        {
            throw new InvalidOperationException("No downloadable update is available for this platform.");
        }

        await _installer.InstallAsync(update.DownloadUrl, progress, cancellationToken);
    }
}
