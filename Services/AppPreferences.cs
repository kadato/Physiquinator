using System.Text.Json;
using Physiquinator.Core.Services;

namespace Physiquinator.Services;

/// <summary>
/// Production <see cref="IAppPreferences"/> implementation backed by MAUI
/// <c>Preferences</c>. In screenshot mode (env var
/// <c>PHYSIQUINATOR_SCREENSHOT_MODE</c>) persists to a JSON file instead so
/// automated UI captures get a clean, isolated preference store.
/// </summary>
public sealed class AppPreferences : IAppPreferences
{
    private readonly bool _isScreenshotMode = Environment.GetEnvironmentVariable("PHYSIQUINATOR_SCREENSHOT_MODE") == "true";
    private readonly Dictionary<string, string> _inMemoryPrefs = [];
    private readonly string? _filePath;

    public AppPreferences()
    {
        var customDbDir = Environment.GetEnvironmentVariable("PHYSIQUINATOR_DB_DIR");
        if (!_isScreenshotMode || string.IsNullOrEmpty(customDbDir))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(customDbDir);
            _filePath = Path.Combine(customDbDir, "screenshot_preferences.json");
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _inMemoryPrefs = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            }
        }
        catch
        {
            // Fallback to empty if file operations fail
        }
    }

    private void Save()
    {
        if (_filePath != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(_inMemoryPrefs);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Ignore
            }
        }
    }

    public string Get(string key, string defaultValue)
    {
        if (_isScreenshotMode)
        {
            return _inMemoryPrefs.TryGetValue(key, out var val) ? val : defaultValue;
        }
        return Microsoft.Maui.Storage.Preferences.Default.Get(key, defaultValue);
    }

    public bool Get(string key, bool defaultValue)
    {
        if (_isScreenshotMode)
        {
            if (key.StartsWith(PreferenceKeys.ShowFirstTimeSeedModal, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return _inMemoryPrefs.TryGetValue(key, out var val) && bool.TryParse(val, out var b) ? b : defaultValue;
        }
        return Microsoft.Maui.Storage.Preferences.Default.Get(key, defaultValue);
    }

    public void Set(string key, string value)
    {
        if (_isScreenshotMode)
        {
            _inMemoryPrefs[key] = value;
            Save();
            return;
        }
        Microsoft.Maui.Storage.Preferences.Default.Set(key, value);
    }

    public void Set(string key, bool value)
    {
        if (_isScreenshotMode)
        {
            _inMemoryPrefs[key] = value.ToString();
            Save();
            return;
        }
        Microsoft.Maui.Storage.Preferences.Default.Set(key, value);
    }
}
