using Microsoft.Extensions.Logging;
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
    private readonly ILogger<GitHubReleaseClient>? _logger;

    public GitHubReleaseClient(HttpClient http, ILogger<GitHubReleaseClient>? logger = null)
    {
        _http = http;
        _logger = logger;
        if (_http.Timeout == Timeout.InfiniteTimeSpan)
            _http.Timeout = TimeSpan.FromSeconds(15);
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Physiquinator-Updater");
        }
    }

    /// <inheritdoc />
    public async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        // Retry transient failures once: 429 and 5xx plus network blips.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using HttpResponseMessage response = await _http.GetAsync(LatestReleaseEndpoint, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if ((int)response.StatusCode is 429 or >= 500)
                {
                    _logger?.LogWarning("GitHub release check got HTTP {StatusCode}, attempt {Attempt}.", (int)response.StatusCode, attempt + 1);
                    if (attempt == 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                        continue;
                    }
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("GitHub release check got HTTP {StatusCode}.", (int)response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                return Parse(json);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(ex, "GitHub release check timed out, attempt {Attempt}.", attempt + 1);
                if (attempt == 0)
                    continue;
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogWarning(ex, "GitHub release check network error, attempt {Attempt}.", attempt + 1);
                if (attempt == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }
                return null;
            }
        }

        return null;
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
