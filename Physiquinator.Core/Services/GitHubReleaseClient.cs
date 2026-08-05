using System.Net;
using System.Text.Json;
using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>Queries the GitHub REST API for Physiquinator release metadata.</summary>
public sealed class GitHubReleaseClient : IGitHubReleaseClient
{
    private static readonly Uri LatestReleaseEndpoint =
        new("https://api.github.com/repos/tothKarolyDavid/Physiquinator/releases/latest");

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
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(json);
    }

    /// <summary>Parses a GitHub releases/latest JSON payload into a <see cref="GitHubRelease"/>.</summary>
    public static GitHubRelease Parse(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
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

                string downloadUrl = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                    ? urlElement.GetString() ?? string.Empty
                    : string.Empty;
                long size = asset.TryGetProperty("size", out JsonElement sizeElement) && sizeElement.TryGetInt64(out long s) ? s : 0;
                assets.Add(new GitHubReleaseAsset(name, downloadUrl, size));
            }
        }

        string tag = root.TryGetProperty("tag_name", out JsonElement tagElement) ? tagElement.GetString() ?? string.Empty : string.Empty;
        string releaseName = root.TryGetProperty("name", out JsonElement releaseNameElement) ? releaseNameElement.GetString() ?? string.Empty : string.Empty;
        string? notes = root.TryGetProperty("body", out JsonElement bodyElement) ? bodyElement.GetString() : null;
        DateTimeOffset? publishedAt = root.TryGetProperty("published_at", out JsonElement publishedElement) &&
                                      publishedElement.TryGetDateTimeOffset(out DateTimeOffset published)
            ? published
            : null;
        bool prerelease = root.TryGetProperty("prerelease", out JsonElement prereleaseElement) &&
                          prereleaseElement.ValueKind == JsonValueKind.True;

        return new GitHubRelease(tag, releaseName, notes, publishedAt, prerelease, assets);
    }
}
