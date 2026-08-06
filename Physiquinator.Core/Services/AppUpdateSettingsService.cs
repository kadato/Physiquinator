namespace Physiquinator.Core.Services;

/// <summary>Persisted preference for the automatic startup update check.</summary>
public sealed class AppUpdateSettingsService(IAppPreferences preferences)
{
    /// <summary>True when the app should check for a newer release on startup.</summary>
    public bool AutoCheckEnabled => preferences.Get(PreferenceKeys.AutoUpdateCheckEnabled, true);

    public event Action? Changed;

    public void SetAutoCheckEnabled(bool enabled)
    {
        preferences.Set(PreferenceKeys.AutoUpdateCheckEnabled, enabled);
        Changed?.Invoke();
    }
}
