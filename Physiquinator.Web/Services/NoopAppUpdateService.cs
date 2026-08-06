using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>Update service for the browser host: in-app updates are not supported, so never report one.</summary>
public sealed class NoopAppUpdateService : IAppUpdateService
{
    public Version CurrentVersion { get; } =
        typeof(NoopAppUpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0);

    public bool IsSupported => false;

    public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new UpdateCheckResult(null, false, null));

    public Task DownloadAndInstallAsync(UpdateCheckResult update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("In-app updates are not supported on this platform.");
}
