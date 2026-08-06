using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>Result of an update check against the GitHub releases.</summary>
public sealed record UpdateCheckResult(GitHubRelease? Latest, bool IsUpdateAvailable, string? DownloadUrl);

/// <summary>Checks for new Physiquinator releases and installs them.</summary>
public interface IAppUpdateService
{
    /// <summary>Version of the installed app.</summary>
    Version CurrentVersion { get; }

    /// <summary>True when in-app updates are supported on the current platform.</summary>
    bool IsSupported { get; }

    /// <summary>Queries GitHub for the latest release and resolves the download for this platform.</summary>
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Downloads the update and hands it to the platform installer.</summary>
    Task DownloadAndInstallAsync(UpdateCheckResult update, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
