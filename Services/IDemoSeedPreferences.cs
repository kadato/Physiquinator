namespace Physiquinator.Services;

/// <summary>Abstraction for one-time demo seed flags (backed by MAUI Preferences in production).</summary>
public interface IDemoSeedPreferences
{
    bool Get(string key, bool defaultValue);

    void Set(string key, bool value);
}
