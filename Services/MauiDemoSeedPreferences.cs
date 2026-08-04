namespace Physiquinator.Services;

public sealed class MauiDemoSeedPreferences : IDemoSeedPreferences
{
    private string GetScopedKey(string key)
    {
        var activeId = AppPreferences.Get(PreferenceKeys.ActiveProfileId, string.Empty);
        if (string.IsNullOrEmpty(activeId) || activeId == UserProfileService.DemoProfileId.ToString())
        {
            return key;
        }
        return $"{key}_{activeId}";
    }

    public bool Get(string key, bool defaultValue) => AppPreferences.Get(GetScopedKey(key), defaultValue);

    public void Set(string key, bool value) => AppPreferences.Set(GetScopedKey(key), value);

    public bool IsDefaultProfile
    {
        get
        {
            var activeId = AppPreferences.Get(PreferenceKeys.ActiveProfileId, string.Empty);
            return string.IsNullOrEmpty(activeId) || activeId == UserProfileService.DemoProfileId.ToString();
        }
    }
}
