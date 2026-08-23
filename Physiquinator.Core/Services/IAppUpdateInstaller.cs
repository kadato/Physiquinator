namespace Physiquinator.Core.Services;

/// <summary>Platform-specific download and install of a Physiquinator release.</summary>
public interface IAppUpdateInstaller
{
    /// <summary>True when in-app updates are supported on the current platform.</summary>
    bool IsSupported { get; }

    /// <summary>Name of the release asset this platform installs, for example the APK or the Windows ZIP.</summary>
    string AssetFileName { get; }

    /// <summary>Downloads the release asset and installs it. Throws when unsupported or installation fails.</summary>
    Task InstallAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
