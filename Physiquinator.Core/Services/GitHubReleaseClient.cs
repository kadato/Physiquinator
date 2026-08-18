using Physiquinator.Core.Models;
using System.Net;
using System.Text.Json;

namespace Physiquinator.Core.Services;

/// <summary>Queries the GitHub REST API for Physiquinator release metadata.</summary>
public sealed class GitHubReleaseClient : IGitHubReleaseClient
{
    private static readonly Uri LatestReleaseEndpoint =
        new("https://api.github.com/repos/kadato/Physiquinator/releases/latest");

    private readonly HttpClient _http;

    public GitHubReleaseClient(HttpClient http)
    {
        _http = http;
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Physiquinator-Updater");
        }
    }

    /// <inheritdoc />
    public async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.GetAsync(LatestReleaseEndpoint, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(json);
    }

    /// <summary>Parses a GitHub releases/latest JSON payload into a <see cref="GitHubRelease"/>.</summary>
    public static GitHubRelease Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        var assets = new List<GitHubReleaseAsset>();
        if (root.TryGetProperty("assets", out JsonElement assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement asset in assetsElement.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out JsonElement nameElement) || nameElement.GetString() is not { } name)
                {
                    continue;
                }

                var downloadUrl = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                    ? urlElement.GetString() ?? string.Empty
                    : string.Empty;
                var size = asset.TryGetProperty("size", out JsonElement sizeElement) && sizeElement.TryGetInt64(out var s) ? s : 0;
                assets.Add(new GitHubReleaseAsset(name, downloadUrl, size));
            }
        }

        var tag = root.TryGetProperty("tag_name", out JsonElement tagElement) ? tagElement.GetString() ?? string.Empty : string.Empty;
        var releaseName = root.TryGetProperty("name", out JsonElement releaseNameElement) ? releaseNameElement.GetString() ?? string.Empty : string.Empty;
        var notes = root.TryGetProperty("body", out JsonElement bodyElement) ? bodyElement.GetString() : null;
        DateTimeOffset? publishedAt = root.TryGetProperty("published_at", out JsonElement publishedElement) &&
                                      publishedElement.TryGetDateTimeOffset(out DateTimeOffset published)
            ? published
            : null;
        var prerelease = root.TryGetProperty("prerelease", out JsonElement prereleaseElement) &&
                          prereleaseElement.ValueKind == JsonValueKind.True;

        return new GitHubRelease(tag, releaseName, notes, publishedAt, prerelease, assets);
    }
}
