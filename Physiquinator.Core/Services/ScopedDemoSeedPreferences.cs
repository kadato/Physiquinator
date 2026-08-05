namespace Physiquinator.Core.Services;

public sealed class ScopedDemoSeedPreferences(IAppPreferences preferences) : IDemoSeedPreferences
{
    private string GetScopedKey(string key)
    {
        var activeId = preferences.Get(PreferenceKeys.ActiveProfileId, string.Empty);
        if (string.IsNullOrEmpty(activeId) || activeId == UserProfileService.DemoProfileId.ToString())
        {
            return key;
        }
        return $"{key}_{activeId}";
    }

    public bool Get(string key, bool defaultValue) => preferences.Get(GetScopedKey(key), defaultValue);

    public void Set(string key, bool value) => preferences.Set(GetScopedKey(key), value);

    public bool IsDefaultProfile
    {
        get
        {
            var activeId = preferences.Get(PreferenceKeys.ActiveProfileId, string.Empty);
            return string.IsNullOrEmpty(activeId) || activeId == UserProfileService.DemoProfileId.ToString();
        }
    }
}
