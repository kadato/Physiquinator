using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Physiquinator.Services;

public static class AppPreferences
{
    private static readonly bool IsScreenshotMode = Environment.GetEnvironmentVariable("PHYSIQUINATOR_SCREENSHOT_MODE") == "true";
    private static readonly string? CustomDbDir = Environment.GetEnvironmentVariable("PHYSIQUINATOR_DB_DIR");
    private static Dictionary<string, string> _inMemoryPrefs = new();
    private static string? _filePath;

    static AppPreferences()
    {
        if (IsScreenshotMode && !string.IsNullOrEmpty(CustomDbDir))
        {
            try
            {
                Directory.CreateDirectory(CustomDbDir);
                _filePath = Path.Combine(CustomDbDir, "screenshot_preferences.json");
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    _inMemoryPrefs = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch
            {
                // Fallback to empty if file operations fail
            }
        }
    }

    private static void Save()
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

    public static string Get(string key, string defaultValue)
    {
        if (IsScreenshotMode)
        {
            return _inMemoryPrefs.TryGetValue(key, out var val) ? val : defaultValue;
        }
        return Microsoft.Maui.Storage.Preferences.Default.Get(key, defaultValue);
    }

    public static bool Get(string key, bool defaultValue)
    {
        if (IsScreenshotMode)
        {
            if (key.StartsWith("Physiquinator.ShowFirstTimeSeedModal", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return _inMemoryPrefs.TryGetValue(key, out var val) && bool.TryParse(val, out var b) ? b : defaultValue;
        }
        return Microsoft.Maui.Storage.Preferences.Default.Get(key, defaultValue);
    }

    public static void Set(string key, string value)
    {
        if (IsScreenshotMode)
        {
            _inMemoryPrefs[key] = value;
            Save();
            return;
        }
        Microsoft.Maui.Storage.Preferences.Default.Set(key, value);
    }

    public static void Set(string key, bool value)
    {
        if (IsScreenshotMode)
        {
            _inMemoryPrefs[key] = value.ToString();
            Save();
            return;
        }
        Microsoft.Maui.Storage.Preferences.Default.Set(key, value);
    }
}
