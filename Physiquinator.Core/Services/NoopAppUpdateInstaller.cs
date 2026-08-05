namespace Physiquinator.Core.Services;

/// <summary>Installer for hosts where in-app updates are not available (e.g. the web preview host).</summary>
public sealed class NoopAppUpdateInstaller : IAppUpdateInstaller
{
    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public string AssetFileName => string.Empty;

    /// <inheritdoc />
    public Task InstallAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("In-app updates are not supported on this platform.");
}
