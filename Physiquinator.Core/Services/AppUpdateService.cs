using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>Shared update-check logic. Downloading and installing is delegated to the platform installer.</summary>
public sealed class AppUpdateService : IAppUpdateService
{
    private readonly IGitHubReleaseClient _client;
    private readonly IAppUpdateInstaller _installer;

    public AppUpdateService(IGitHubReleaseClient client, IAppUpdateInstaller installer, Version currentVersion)
    {
        _client = client;
        _installer = installer;
        CurrentVersion = currentVersion;
    }

    /// <inheritdoc />
    public Version CurrentVersion { get; }

    /// <inheritdoc />
    public bool IsSupported => _installer.IsSupported;

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        GitHubRelease? release = await _client.GetLatestReleaseAsync(cancellationToken);
        if (release is null)
        {
            return new UpdateCheckResult(null, false, null);
        }

        var isUpdateAvailable = release.IsNewerThan(CurrentVersion);
        if (!isUpdateAvailable)
        {
            return new UpdateCheckResult(release, false, null);
        }

        GitHubReleaseAsset? asset = _installer.AssetFileName is { Length: > 0 }
            ? release.Assets.FirstOrDefault(a => string.Equals(a.Name, _installer.AssetFileName, StringComparison.OrdinalIgnoreCase))
            : null;

        return new UpdateCheckResult(release, true, asset?.DownloadUrl);
    }

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
