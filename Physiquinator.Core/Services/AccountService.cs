namespace Physiquinator.Core.Services;

/// <summary>
/// Account actions exposed by hosted builds. Local app builds report no
/// supported actions so the UI can hide sign-out entirely.
/// </summary>
public interface IAccountService
{
    /// <summary>True when this build exposes account actions (the hosted web app with accounts).</summary>
    bool IsSignOutSupported { get; }

    Task SignOutAsync();
}

/// <summary>Default no-op for local app builds without accounts.</summary>
public sealed class NoopAccountService : IAccountService
{
    public bool IsSignOutSupported => false;

    public Task SignOutAsync() => Task.CompletedTask;
}
