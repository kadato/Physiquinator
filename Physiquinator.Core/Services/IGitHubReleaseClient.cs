using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>Reads release metadata for the Physiquinator GitHub repository.</summary>
public interface IGitHubReleaseClient
{
    /// <summary>Fetches the newest published release, or null when the repository has no releases.</summary>
    Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);
}
