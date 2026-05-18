using Microsoft.Maui.Storage;

namespace Physiquinator.Services;

public sealed class MauiDemoSeedPreferences : IDemoSeedPreferences
{
    public bool Get(string key, bool defaultValue) => Preferences.Default.Get(key, defaultValue);

    public void Set(string key, bool value) => Preferences.Default.Set(key, value);
}
