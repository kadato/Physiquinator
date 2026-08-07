using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using System.Net;
using Xunit;

namespace Physiquinator.Tests.Services;

public class GitHubReleaseClientTests
{
    private const string SampleReleaseJson = """
        {
          "url": "https://api.github.com/repos/tothKarolyDavid/Physiquinator/releases/42",
          "html_url": "https://github.com/tothKarolyDavid/Physiquinator/releases/tag/v1.2.0",
          "tag_name": "v1.2.0",
          "name": "Physiquinator v1.2.0",
          "draft": false,
          "prerelease": false,
          "published_at": "2026-07-01T10:30:00Z",
          "body": "Release notes here.",
          "assets": [
            {
              "name": "Physiquinator-Android.apk",
              "browser_download_url": "https://github.com/tothKarolyDavid/Physiquinator/releases/download/v1.2.0/Physiquinator-Android.apk",
              "size": 31457280
            },
            {
              "name": "Physiquinator-Windows.zip",
              "browser_download_url": "https://github.com/tothKarolyDavid/Physiquinator/releases/download/v1.2.0/Physiquinator-Windows.zip",
              "size": 65011712
            }
          ]
        }
        """;

    [Fact]
    public void Parse_CompleteJson_MapsReleaseAndAssets()
    {
        GitHubRelease release = GitHubReleaseClient.Parse(SampleReleaseJson);

        Assert.Equal("v1.2.0", release.Tag);
        Assert.Equal("Physiquinator v1.2.0", release.Name);
        Assert.Equal("Release notes here.", release.Notes);
        Assert.False(release.IsPrerelease);
        Assert.Equal(2, release.Assets.Count);
        Assert.Equal(GitHubReleaseAssets.AndroidApk, release.Assets[0].Name);
        Assert.EndsWith("/Physiquinator-Android.apk", release.Assets[0].DownloadUrl);
        Assert.Equal(31457280, release.Assets[0].SizeBytes);
        Assert.Equal(GitHubReleaseAssets.WindowsZip, release.Assets[1].Name);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 10, 30, 0, TimeSpan.Zero), release.PublishedAt);
    }

    [Fact]
    public void Parse_ReleaseWithoutAssets_ReturnsEmptyAssets()
    {
        const string json = """{"tag_name":"v1.0.0","name":"x","published_at":"2026-01-01T00:00:00Z","assets":[]}""";

        GitHubRelease release = GitHubReleaseClient.Parse(json);

        Assert.Empty(release.Assets);
        Assert.Equal("v1.0.0", release.Tag);
    }

    [Fact]
    public void Parse_MissingFields_DoesNotThrow()
    {
        GitHubRelease release = GitHubReleaseClient.Parse("{}");

        Assert.Equal(string.Empty, release.Tag);
        Assert.Null(release.Version);
        Assert.Null(release.Notes);
        Assert.Null(release.PublishedAt);
        Assert.Empty(release.Assets);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_WhenNotFound_ReturnsNull()
    {
        GitHubReleaseClient client = CreateClient(() => new HttpResponseMessage(HttpStatusCode.NotFound));

        GitHubRelease? release = await client.GetLatestReleaseAsync();

        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_WhenSuccess_ReturnsParsedRelease()
    {
        GitHubReleaseClient client = CreateClient(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleReleaseJson)
        });

        GitHubRelease? release = await client.GetLatestReleaseAsync();

        Assert.NotNull(release);
        Assert.Equal("v1.2.0", release.Tag);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_WhenServerError_Throws()
    {
        GitHubReleaseClient client = CreateClient(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetLatestReleaseAsync());
    }

    [Fact]
    public async Task GetLatestReleaseAsync_SendsUserAgentHeader()
    {
        HttpRequestMessage? captured = null;
        GitHubReleaseClient client = CreateClient(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await client.GetLatestReleaseAsync();

        Assert.NotNull(captured);
        Assert.Contains(captured.Headers.UserAgent, ua => ua.Product?.Name == "Physiquinator-Updater");
    }

    private static GitHubReleaseClient CreateClient(Func<HttpResponseMessage> responder) =>
        CreateClient(_ => responder());

    private static GitHubReleaseClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new StubHttpMessageHandler(responder)));

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
