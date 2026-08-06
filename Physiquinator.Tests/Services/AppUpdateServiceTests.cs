using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Xunit;

namespace Physiquinator.Tests.Services;

public class AppUpdateServiceTests
{
    private static readonly Version Installed = new(1, 1, 0);

    private static GitHubRelease Release(string tag, params GitHubReleaseAsset[] assets) =>
        new(tag, $"Physiquinator {tag}", "notes", DateTimeOffset.UtcNow, false, assets);

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNoReleaseExists_ReturnsNoUpdate()
    {
        var sut = new AppUpdateService(new FakeReleaseClient(null), new FakeInstaller("apk"), Installed);

        UpdateCheckResult result = await sut.CheckForUpdatesAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.Null(result.Latest);
        Assert.Null(result.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenInstalledVersionIsLatest_ReturnsNoUpdate()
    {
        var client = new FakeReleaseClient(Release("v1.1.0", new GitHubReleaseAsset(GitHubReleaseAssets.AndroidApk, "https://x/apk", 1)));
        var sut = new AppUpdateService(client, new FakeInstaller(GitHubReleaseAssets.AndroidApk), Installed);

        UpdateCheckResult result = await sut.CheckForUpdatesAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.Latest);
        Assert.Null(result.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNewerReleaseAndAssetMatches_ReturnsDownloadUrl()
    {
        var client = new FakeReleaseClient(Release("v1.2.0", new GitHubReleaseAsset(GitHubReleaseAssets.AndroidApk, "https://x/Physiquinator-Android.apk", 1)));
        var sut = new AppUpdateService(client, new FakeInstaller(GitHubReleaseAssets.AndroidApk), Installed);

        UpdateCheckResult result = await sut.CheckForUpdatesAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("https://x/Physiquinator-Android.apk", result.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenAssetNameMatchesCaseInsensitively_ReturnsDownloadUrl()
    {
        var client = new FakeReleaseClient(Release("v1.2.0", new GitHubReleaseAsset("physiquinator-android.apk", "https://x/apk", 1)));
        var sut = new AppUpdateService(client, new FakeInstaller(GitHubReleaseAssets.AndroidApk), Installed);

        UpdateCheckResult result = await sut.CheckForUpdatesAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("https://x/apk", result.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNewerReleaseButNoMatchingAsset_ReturnsNullDownloadUrl()
    {
        var client = new FakeReleaseClient(Release("v1.2.0", new GitHubReleaseAsset("other.zip", "https://x/other", 1)));
        var sut = new AppUpdateService(client, new FakeInstaller(GitHubReleaseAssets.WindowsZip), Installed);

        UpdateCheckResult result = await sut.CheckForUpdatesAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenInstallerUnsupported_ReturnsNullDownloadUrl()
    {
        var client = new FakeReleaseClient(Release("v1.2.0", new GitHubReleaseAsset(GitHubReleaseAssets.AndroidApk, "https://x/apk", 1)));
        var sut = new AppUpdateService(client, new FakeInstaller("", isSupported: false), Installed);

        UpdateCheckResult result = await sut.CheckForUpdatesAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenReleaseTagIsNotAVersion_ReturnsNoUpdate()
    {
        var client = new FakeReleaseClient(Release("not-a-version"));
        var sut = new AppUpdateService(client, new FakeInstaller(GitHubReleaseAssets.AndroidApk), Installed);

        UpdateCheckResult result = await sut.CheckForUpdatesAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.Null(result.DownloadUrl);
    }

    [Fact]
    public async Task DownloadAndInstallAsync_WithoutDownloadUrl_Throws()
    {
        var sut = new AppUpdateService(new FakeReleaseClient(null), new FakeInstaller(GitHubReleaseAssets.AndroidApk), Installed);
        var update = new UpdateCheckResult(Release("v1.2.0"), true, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DownloadAndInstallAsync(update));
    }

    [Fact]
    public async Task DownloadAndInstallAsync_WithDownloadUrl_DelegatesToInstaller()
    {
        var installer = new FakeInstaller(GitHubReleaseAssets.AndroidApk);
        var sut = new AppUpdateService(new FakeReleaseClient(null), installer, Installed);
        var update = new UpdateCheckResult(Release("v1.2.0"), true, "https://x/apk");

        await sut.DownloadAndInstallAsync(update);

        Assert.Equal("https://x/apk", installer.InstalledUrl);
        Assert.Equal(1, installer.InstallCallCount);
    }

    [Fact]
    public async Task DownloadAndInstallAsync_WhenInstallerUnsupported_Throws()
    {
        var sut = new AppUpdateService(new FakeReleaseClient(null), new FakeInstaller("", isSupported: false), Installed);
        var update = new UpdateCheckResult(Release("v1.2.0"), true, "https://x/apk");

        await Assert.ThrowsAsync<NotSupportedException>(() => sut.DownloadAndInstallAsync(update));
    }

    private sealed class FakeReleaseClient : IGitHubReleaseClient
    {
        private readonly GitHubRelease? _release;

        public FakeReleaseClient(GitHubRelease? release)
        {
            _release = release;
        }

        public Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_release);
    }

    private sealed class FakeInstaller : IAppUpdateInstaller
    {
        private readonly bool _isSupported;

        public FakeInstaller(string assetFileName, bool isSupported = true)
        {
            AssetFileName = assetFileName;
            _isSupported = isSupported;
        }

        public bool IsSupported => _isSupported;

        public string AssetFileName { get; }

        public string? InstalledUrl { get; private set; }

        public int InstallCallCount { get; private set; }

        public Task InstallAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!_isSupported)
            {
                throw new NotSupportedException("In-app updates are not supported on this platform.");
            }

            InstalledUrl = downloadUrl;
            InstallCallCount++;
            return Task.CompletedTask;
        }
    }
}
